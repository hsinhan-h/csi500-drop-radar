using System.Text.Json;
using IndexSwingRadar.Models;

namespace IndexSwingRadar.Services.Indices.UsCommon;

/// <summary>
/// 從 Yahoo Finance v8 chart API 取得美股前複權日 K 線。
/// 使用 cookie + crumb 認證機制（Yahoo 2024 後要求）。
/// </summary>
public class YahooQuoteProvider : IQuoteProvider, IDisposable
{
    private static readonly Polly.ResiliencePipeline<System.Net.Http.HttpResponseMessage> _retryPipeline =
        CommonHttp.CreateHttpRetryPipeline();

    private readonly HttpClient _http;
    private string? _crumb;
    private readonly SemaphoreSlim _crumbLock = new(1, 1);
    private const string YahooQuery2 = "https://query2.finance.yahoo.com";
    private const string YahooQuery1 = "https://query1.finance.yahoo.com";

    public YahooQuoteProvider()
    {
        // Render 等平台偶發 Yahoo 路由差異：優先固定 IPv4，並保留 cookie 以支援 crumb fallback。
        _http = CommonHttp.CreateIpv4Client(useCookies: true);
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _http.DefaultRequestHeaders.Add("Accept", "application/json,*/*");
        _http.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
    }

    public async Task<StockRecord?> FetchAsync(
        StockSymbol symbol,
        DateTime startDate,
        DateTime endDate,
        CancellationToken ct = default)
    {
        try
        {
            var p1 = new DateTimeOffset(startDate.Date, TimeSpan.Zero).ToUnixTimeSeconds();
            var p2 = new DateTimeOffset(endDate.Date.AddDays(1), TimeSpan.Zero).ToUnixTimeSeconds();
            var basePath = $"/v8/finance/chart/{Uri.EscapeDataString(symbol.Code)}" +
                           $"?period1={p1}&period2={p2}&interval=1d";

            // 先嘗試不帶 crumb；Polly 負責 429/5xx 的指數退避重試。
            var resp = await _retryPipeline.ExecuteAsync(
                async token => await _http.GetAsync($"{YahooQuery2}{basePath}", token), ct);

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                resp = await _retryPipeline.ExecuteAsync(
                    async token => await _http.GetAsync($"{YahooQuery1}{basePath}", token), ct);

            // 被拒絕時才啟用 cookie + crumb fallback。
            if (resp.StatusCode is System.Net.HttpStatusCode.Unauthorized
                                or System.Net.HttpStatusCode.Forbidden)
            {
                _crumb = null;
                var fallbackJson = await TryFetchWithCrumbFallbackAsync(basePath, ct);
                return fallbackJson != null ? ParseChart(symbol, fallbackJson) : null;
            }

            resp.EnsureSuccessStatusCode();
            return ParseChart(symbol, await resp.Content.ReadAsStringAsync(ct));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] Yahoo {symbol.Code} 失敗: {ex.Message}");
            return null;
        }
    }

    private async Task<string?> TryFetchWithCrumbFallbackAsync(string basePath, CancellationToken ct)
    {
        var crumb = await EnsureCrumbAsync(ct);
        var withCrumb = $"{basePath}&crumb={Uri.EscapeDataString(crumb)}";

        foreach (var host in new[] { YahooQuery2, YahooQuery1 })
        {
            var resp = await _retryPipeline.ExecuteAsync(
                async token => await _http.GetAsync($"{host}{withCrumb}", token), ct);
            if (resp.IsSuccessStatusCode)
                return await resp.Content.ReadAsStringAsync(ct);
        }

        return null;
    }

    // ── 取得 / 快取 Yahoo crumb（429/5xx 由 Polly 指數退避處理）──────────
    private async Task<string> EnsureCrumbAsync(CancellationToken ct)
    {
        if (_crumb != null) return _crumb;

        await _crumbLock.WaitAsync(ct);
        try
        {
            if (_crumb != null) return _crumb;
            await _http.GetAsync("https://fc.yahoo.com/", ct);

            foreach (var host in new[] { YahooQuery2, YahooQuery1 })
            {
                var resp = await _retryPipeline.ExecuteAsync(
                    async token => await _http.GetAsync($"{host}/v1/test/getcrumb", token), ct);

                if (resp.IsSuccessStatusCode)
                {
                    _crumb = await resp.Content.ReadAsStringAsync(ct);
                    if (!string.IsNullOrWhiteSpace(_crumb)) return _crumb;
                }
            }

            throw new Exception("Yahoo Finance crumb 取得失敗");
        }
        finally
        {
            _crumbLock.Release();
        }
    }

    // ── 解析 chart JSON ───────────────────────────────────────────────────
    private static StockRecord? ParseChart(StockSymbol symbol, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var chartNode = doc.RootElement.GetProperty("chart");

        if (chartNode.TryGetProperty("error", out var err) &&
            err.ValueKind != JsonValueKind.Null)
        {
            Console.WriteLine($"[WARN] Yahoo {symbol.Code}: {err}");
            return null;
        }

        var result = chartNode.GetProperty("result");
        if (result.ValueKind == JsonValueKind.Null || result.GetArrayLength() == 0) return null;

        var r = result[0];
        if (!r.TryGetProperty("timestamp", out var tsNode)) return null;

        var timestamps = tsNode.EnumerateArray().ToList();

        // adjclose 在部分 ticker 的回應中可能缺失
        if (!r.TryGetProperty("indicators", out var indicators)) return null;
        if (!indicators.TryGetProperty("adjclose", out var adjcloseArr)) return null;
        if (adjcloseArr.GetArrayLength() == 0) return null;
        if (!adjcloseArr[0].TryGetProperty("adjclose", out var adjcloseNode)) return null;

        var adjCloses = adjcloseNode.EnumerateArray().ToList();

        if (timestamps.Count == 0 || adjCloses.Count < timestamps.Count) return null;

        double? startClose = null, endClose = null;
        string? startDateStr = null, endDateStr = null;

        for (int i = 0; i < timestamps.Count; i++)
        {
            if (adjCloses[i].ValueKind == JsonValueKind.Null) continue;
            var close = adjCloses[i].GetDouble();
            if (close <= 0) continue;

            var date = DateTimeOffset.FromUnixTimeSeconds(timestamps[i].GetInt64())
                                     .UtcDateTime.ToString("yyyy-MM-dd");
            if (startClose == null) { startClose = close; startDateStr = date; }
            endClose   = close;
            endDateStr = date;
        }

        if (startClose == null || endClose == null) return null;

        return new StockRecord
        {
            Code       = symbol.Code,
            Name       = symbol.Name,
            StartClose = Math.Round(startClose.Value, 2),
            EndClose   = Math.Round(endClose.Value,   2),
            PctChange  = Math.Round((endClose.Value - startClose.Value) / startClose.Value * 100, 2),
            StartDate  = startDateStr ?? "",
            EndDate    = endDateStr   ?? "",
        };
    }

    public void Dispose() => _http.Dispose();
}
