using System.Text.Json;

namespace IndexSwingRadar.Services.Indices.Ndx;

/// <summary>
/// 從 Yahoo Finance quoteSummary API 取得 Nasdaq-100（NDX）成分股清單。
/// Step 1：GET quoteSummary/^NDX?modules=components  → ticker 清單
/// Step 2：GET v7/finance/quote?symbols=...          → 公司名稱
/// 使用與 YahooQuoteProvider 相同的 cookie + crumb 認證機制。
/// </summary>
public class NasdaqNdxConstituentProvider : IConstituentProvider
{
    private readonly HttpClient _http;
    private string? _crumb;
    private readonly SemaphoreSlim _crumbLock = new(1, 1);

    public NasdaqNdxConstituentProvider()
    {
        _http = CommonHttp.CreateIpv4Client(useCookies: true);
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _http.DefaultRequestHeaders.Add("Accept", "application/json,*/*");
        _http.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
    }

    public async Task<IReadOnlyList<StockSymbol>> FetchAsync(CancellationToken ct = default)
    {
        // crumb 失效時最多重新取得一次
        for (int attempt = 0; attempt < 2; attempt++)
        {
            var crumb = await EnsureCrumbAsync(ct);

            // ── Step 1：取得 NDX 成分股 ticker 清單 ───────────────────────
            var componentsUrl =
                "https://query2.finance.yahoo.com/v10/finance/quoteSummary/%5ENDX" +
                $"?modules=components&crumb={Uri.EscapeDataString(crumb)}";

            var (ok1, componentsJson) = await TryGetAsync(componentsUrl, ct);
            if (!ok1)
            {
                _crumb = null;   // 強制下次重取 crumb
                continue;
            }

            var tickers = ParseComponents(componentsJson!);
            if (tickers.Count < 90)
                throw new InvalidOperationException(
                    $"NDX components 僅取得 {tickers.Count} 筆，預期約 100 筆。" +
                    $"原始回應（前 300 字）：{componentsJson![..Math.Min(300, componentsJson.Length)]}");

            // ── Step 2：批次取公司名稱 ────────────────────────────────────
            var symbols    = string.Join(",", tickers.Select(Uri.EscapeDataString));
            var quotesUrl  =
                "https://query2.finance.yahoo.com/v7/finance/quote" +
                $"?symbols={symbols}&crumb={Uri.EscapeDataString(crumb)}";

            var (ok2, quotesJson) = await TryGetAsync(quotesUrl, ct);
            var nameMap = ok2 ? ParseNames(quotesJson!) : new Dictionary<string, string>();

            return tickers
                .Select(t => new StockSymbol(t, nameMap.TryGetValue(t, out var n) ? n : t))
                .ToList();
        }

        throw new InvalidOperationException("Yahoo Finance NDX 成分股抓取失敗（crumb 認證連續失敗）。");
    }

    // ── 解析 components ───────────────────────────────────────────────────
    private static List<string> ParseComponents(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var result = doc.RootElement
            .GetProperty("quoteSummary")
            .GetProperty("result");

        if (result.ValueKind == JsonValueKind.Null || result.GetArrayLength() == 0)
            throw new InvalidOperationException(
                $"quoteSummary result 為空。原始回應（前 300 字）：{json[..Math.Min(300, json.Length)]}");

        var components = result[0]
            .GetProperty("components")
            .GetProperty("components");

        return components.EnumerateArray()
            .Select(e => e.GetString()?.Trim() ?? "")
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();
    }

    // ── 解析公司名稱 ──────────────────────────────────────────────────────
    private static Dictionary<string, string> ParseNames(string json)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var quotes = doc.RootElement
                .GetProperty("quoteResponse")
                .GetProperty("result");

            foreach (var q in quotes.EnumerateArray())
            {
                var symbol = q.TryGetProperty("symbol",    out var s) ? s.GetString() ?? "" : "";
                var name   = q.TryGetProperty("shortName", out var n) ? n.GetString() ?? symbol : symbol;
                if (!string.IsNullOrEmpty(symbol))
                    map[symbol] = name;
            }
        }
        catch { /* 名稱抓取失敗時 fallback 到 ticker */ }
        return map;
    }

    // ── HTTP GET（處理 401/403 → 回傳 false 讓外層重試） ─────────────────
    private async Task<(bool ok, string? body)> TryGetAsync(string url, CancellationToken ct)
    {
        try
        {
            var resp = await _http.GetAsync(url, ct);
            if (resp.StatusCode is System.Net.HttpStatusCode.Unauthorized
                                or System.Net.HttpStatusCode.Forbidden)
                return (false, null);

            resp.EnsureSuccessStatusCode();
            return (true, await resp.Content.ReadAsStringAsync(ct));
        }
        catch
        {
            return (false, null);
        }
    }

    // ── cookie + crumb（與 YahooQuoteProvider 相同機制）──────────────────
    private async Task<string> EnsureCrumbAsync(CancellationToken ct)
    {
        if (_crumb != null) return _crumb;

        await _crumbLock.WaitAsync(ct);
        try
        {
            if (_crumb != null) return _crumb;
            await _http.GetAsync("https://fc.yahoo.com/", ct);
            _crumb = await _http.GetStringAsync(
                "https://query2.finance.yahoo.com/v1/test/getcrumb", ct);
            return _crumb;
        }
        finally
        {
            _crumbLock.Release();
        }
    }
}
