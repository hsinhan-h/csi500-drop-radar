using System.Text.RegularExpressions;

namespace IndexSwingRadar.Services.Indices.Sox;

/// <summary>
/// 從 MoneyDJ 理財網爬取 SOXX 全部持股清單。
/// 資料來源：https://www.moneydj.com/ETF/X/Basic/Basic0007B.xdjhtm?etfid=SOXX
/// 頁面以靜態 HTML 呈現，無 bot 防護。
/// </summary>
public class MoneyDjSoxxConstituentProvider : IConstituentProvider
{
    private const string Url =
        "https://www.moneydj.com/ETF/X/Basic/Basic0007B.xdjhtm?etfid=SOXX";

    // 比對 etfid=AMD.US&back=SOXX'>AMD(AMD.US)</a>
    private static readonly Regex HoldingPattern = new(
        @"etfid=([A-Z0-9]+)\.US[^>]+>([^(]+)\([^)]+\)</a>",
        RegexOptions.Compiled);

    private readonly HttpClient _http;

    public MoneyDjSoxxConstituentProvider()
    {
        _http = CommonHttp.CreateIpv4Client();
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    }

    public async Task<IReadOnlyList<StockSymbol>> FetchAsync(CancellationToken ct = default)
    {
        var html = await CommonHttp.RetryGetAsync(_http, Url, ct: ct);
        return ParseHtml(html);
    }

    private static IReadOnlyList<StockSymbol> ParseHtml(string html)
    {
        var results = HoldingPattern.Matches(html)
            .Select(m => new StockSymbol(
                Code: m.Groups[1].Value,
                Name: m.Groups[2].Value.Trim()))
            .DistinctBy(s => s.Code)
            .ToList();

        if (results.Count == 0)
            throw new InvalidOperationException(
                "MoneyDJ SOXX 持股解析失敗：未找到任何成分股，頁面格式可能已變更。");

        return results;
    }
}
