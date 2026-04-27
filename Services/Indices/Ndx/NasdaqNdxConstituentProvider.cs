using System.Text.Json;

namespace IndexSwingRadar.Services.Indices.Ndx;

/// <summary>
/// 從 Nasdaq 官方 API 取得 Nasdaq-100（NDX）成分股清單。
/// GET https://api.nasdaq.com/api/quote/list-type/nasdaq100
/// 不需要任何認證，回傳 JSON 含 data.data.rows[].symbol / companyName。
/// </summary>
public class NasdaqNdxConstituentProvider : IConstituentProvider
{
    private const string ApiUrl = "https://api.nasdaq.com/api/quote/list-type/nasdaq100";

    private readonly HttpClient _http;

    public NasdaqNdxConstituentProvider()
    {
        _http = CommonHttp.CreateIpv4Client();
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _http.DefaultRequestHeaders.Add("Accept", "application/json,*/*");
    }

    public async Task<IReadOnlyList<StockSymbol>> FetchAsync(CancellationToken ct = default)
    {
        var json = await CommonHttp.RetryGetAsync(_http, ApiUrl, ct: ct);
        return ParseJson(json);
    }

    private static List<StockSymbol> ParseJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var rows = doc.RootElement
            .GetProperty("data")
            .GetProperty("data")
            .GetProperty("rows");

        var results = rows.EnumerateArray()
            .Select(r =>
            {
                var symbol = r.TryGetProperty("symbol",      out var s) ? s.GetString() ?? "" : "";
                var name   = r.TryGetProperty("companyName", out var n) ? n.GetString() ?? symbol : symbol;
                return (symbol, name);
            })
            .Where(x => !string.IsNullOrEmpty(x.symbol))
            .Select(x => new StockSymbol(x.symbol, x.name))
            .ToList();

        if (results.Count < 90)
            throw new InvalidOperationException(
                $"Nasdaq NDX API 回傳僅 {results.Count} 筆，預期約 100 筆。API 格式可能已變更。");

        return results;
    }
}
