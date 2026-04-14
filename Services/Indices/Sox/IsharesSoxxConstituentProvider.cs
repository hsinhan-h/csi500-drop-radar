namespace Csi500DropRadar.Services.Indices.Sox;

/// <summary>
/// 從 iShares SOXX ETF 每日持股 CSV 取得 PHLX 半導體指數成分股。
/// CSV 格式：前幾行為元資料，找到含 "Ticker" 欄位的標頭行後開始解析，
/// 篩選 Asset Class == "Equity"。
/// </summary>
public class IsharesSoxxConstituentProvider : IConstituentProvider
{
    private const string CsvUrl =
        "https://www.ishares.com/us/products/239705/" +
        "ishares-phlx-semiconductor-etf/1467271812596.ajax" +
        "?fileType=csv&fileName=SOXX_holdings&dataType=fund";

    private readonly HttpClient _http;

    public IsharesSoxxConstituentProvider()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _http.DefaultRequestHeaders.Add("Referer", "https://www.ishares.com/");
    }

    public async Task<IReadOnlyList<StockSymbol>> FetchAsync(CancellationToken ct = default)
    {
        var csv = await CommonHttp.RetryGetAsync(_http, CsvUrl, ct: ct);
        return ParseCsv(csv);
    }

    private static IReadOnlyList<StockSymbol> ParseCsv(string csv)
    {
        var results = new List<StockSymbol>();
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        int tickerCol = -1, nameCol = -1, assetClassCol = -1;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            var cols = SplitCsvLine(line);

            // 尋找標頭行（含 "Ticker" 欄位）
            if (tickerCol == -1)
            {
                for (int i = 0; i < cols.Count; i++)
                {
                    var h = cols[i].Trim('"').Trim();
                    if (h.Equals("Ticker",     StringComparison.OrdinalIgnoreCase)) tickerCol     = i;
                    if (h.Equals("Name",       StringComparison.OrdinalIgnoreCase)) nameCol       = i;
                    if (h.Equals("Asset Class",StringComparison.OrdinalIgnoreCase)) assetClassCol = i;
                }
                continue;
            }

            if (cols.Count <= Math.Max(tickerCol, Math.Max(nameCol, assetClassCol))) continue;

            var assetClass = assetClassCol >= 0 ? cols[assetClassCol].Trim('"').Trim() : "Equity";
            if (!assetClass.Equals("Equity", StringComparison.OrdinalIgnoreCase)) continue;

            var ticker = cols[tickerCol].Trim('"').Trim();
            var name   = nameCol >= 0 ? cols[nameCol].Trim('"').Trim() : ticker;

            if (!string.IsNullOrEmpty(ticker) && ticker != "-")
                results.Add(new StockSymbol(ticker, name));
        }

        if (results.Count == 0)
            throw new InvalidOperationException(
                "SOXX CSV 解析失敗：未找到任何 Equity 成分股。" +
                "iShares CSV 格式可能已變更，請確認欄位名稱。");

        return results;
    }

    // 簡易 CSV 欄位分割（處理雙引號包覆）
    private static List<string> SplitCsvLine(string line)
    {
        var cols = new List<string>();
        bool inQuote = false;
        var cur = new System.Text.StringBuilder();

        foreach (char c in line)
        {
            if (c == '"') { inQuote = !inQuote; cur.Append(c); }
            else if (c == ',' && !inQuote) { cols.Add(cur.ToString()); cur.Clear(); }
            else cur.Append(c);
        }
        cols.Add(cur.ToString());
        return cols;
    }
}
