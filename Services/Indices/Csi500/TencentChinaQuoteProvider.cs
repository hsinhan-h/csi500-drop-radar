using System.Text.Json;
using System.Text.RegularExpressions;
using Csi500DropRadar.Models;

namespace Csi500DropRadar.Services.Indices.Csi500;

/// <summary>從騰訊財經取得個股前複權日 K 線（qfq/day），計算期間漲跌幅。</summary>
public class TencentChinaQuoteProvider : IQuoteProvider
{
    private readonly HttpClient _http;

    public TencentChinaQuoteProvider()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0 Safari/537.36");
        _http.DefaultRequestHeaders.Add("Referer", "https://gu.qq.com/");
    }

    public async Task<StockRecord?> FetchAsync(
        StockSymbol symbol,
        DateTime startDate,
        DateTime endDate,
        CancellationToken ct = default)
    {
        var prefix = symbol.Code.StartsWith("6") ? "sh" : "sz";
        var r = new Random().NextDouble();
        var url = $"https://web.ifzq.gtimg.cn/appstock/app/fqkline/get" +
                  $"?_var=kline_dayqfq&param={prefix}{symbol.Code},day," +
                  $"{startDate:yyyy-MM-dd},{endDate:yyyy-MM-dd},500,qfq&r={r:F6}";

        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var raw = await _http.GetStringAsync(url, cts.Token);

                // 去掉 JSONP 外殼 kline_dayqfq={...}
                var jsonStr = Regex.Match(raw, @"=(\{.*\})$", RegexOptions.Singleline).Groups[1].Value;
                if (string.IsNullOrEmpty(jsonStr)) return null;

                using var doc = JsonDocument.Parse(jsonStr);
                var root = doc.RootElement;
                if (root.GetProperty("code").GetInt32() != 0) return null;

                var dataNode = root.GetProperty("data").GetProperty($"{prefix}{symbol.Code}");
                JsonElement klines;
                if (!dataNode.TryGetProperty("qfqday", out klines) &&
                    !dataNode.TryGetProperty("day", out klines))
                    return null;

                var arr = klines.EnumerateArray().ToList();
                if (arr.Count < 2) return null;

                var first = arr[0].EnumerateArray().ToList();
                var last  = arr[^1].EnumerateArray().ToList();

                var startClose = double.Parse(first[2].GetString() ?? "0");
                var endClose   = double.Parse(last[2].GetString()  ?? "0");
                if (startClose <= 0) return null;

                return new StockRecord
                {
                    Code       = symbol.Code,
                    Name       = symbol.Name,
                    StartClose = Math.Round(startClose, 2),
                    EndClose   = Math.Round(endClose,   2),
                    PctChange  = Math.Round((endClose - startClose) / startClose * 100, 2),
                    StartDate  = first[0].GetString()?[..10] ?? "",
                    EndDate    = last[0].GetString()?[..10]  ?? "",
                };
            }
            catch (Exception ex)
            {
                var wait = 2 * (attempt + 1);
                Console.WriteLine($"[WARN] {symbol.Code} 第{attempt + 1}次失敗，等待{wait}s: {ex.Message}");
                await Task.Delay(wait * 1000, ct);
            }
        }

        return null;
    }
}
