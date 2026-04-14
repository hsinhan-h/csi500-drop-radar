using System.Text.Json;
using Csi500DropRadar.Models;

namespace Csi500DropRadar.Services.Indices.UsCommon;

/// <summary>
/// 從 Yahoo Finance v8 chart API 取得美股前複權日 K 線。
/// 使用 cookie + crumb 認證機制（Yahoo 2024 後要求）。
/// </summary>
public class YahooQuoteProvider : IQuoteProvider, IDisposable
{
    private readonly HttpClient _http;
    private string? _crumb;
    private readonly SemaphoreSlim _crumbLock = new(1, 1);

    public YahooQuoteProvider()
    {
        // UseCookies = true 讓 HttpClient 自動保留 fc.yahoo.com 設定的 cookie
        var handler = new HttpClientHandler { UseCookies = true };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
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
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var crumb = await EnsureCrumbAsync(ct);
                var p1 = new DateTimeOffset(startDate.Date, TimeSpan.Zero).ToUnixTimeSeconds();
                var p2 = new DateTimeOffset(endDate.Date.AddDays(1), TimeSpan.Zero).ToUnixTimeSeconds();
                var url = $"https://query2.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(symbol.Code)}" +
                          $"?period1={p1}&period2={p2}&interval=1d" +
                          $"&crumb={Uri.EscapeDataString(crumb)}";

                var resp = await _http.GetAsync(url, ct);

                // crumb 過期時重置並重試
                if (resp.StatusCode is System.Net.HttpStatusCode.Unauthorized
                                    or System.Net.HttpStatusCode.Forbidden)
                {
                    _crumb = null;
                    await Task.Delay(2000 * (attempt + 1), ct);
                    continue;
                }

                var json = await resp.Content.ReadAsStringAsync(ct);
                return ParseChart(symbol, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] Yahoo {symbol.Code} 第{attempt + 1}次失敗: {ex.Message}");
                if (attempt < 2) await Task.Delay(2000 * (attempt + 1), ct);
            }
        }

        return null;
    }

    // ── 取得 / 快取 Yahoo crumb ───────────────────────────────────────────
    private async Task<string> EnsureCrumbAsync(CancellationToken ct)
    {
        if (_crumb != null) return _crumb;

        await _crumbLock.WaitAsync(ct);
        try
        {
            if (_crumb != null) return _crumb;

            // Step 1：打 fc.yahoo.com 讓伺服器設定 session cookie
            await _http.GetAsync("https://fc.yahoo.com/", ct);

            // Step 2：取得 crumb 字串
            _crumb = await _http.GetStringAsync(
                "https://query2.finance.yahoo.com/v1/test/getcrumb", ct);

            return _crumb;
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
