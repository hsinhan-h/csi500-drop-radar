using System.Text.Json;

namespace Csi500DropRadar.Services.Indices.Csi500;

/// <summary>從東方財富 push2 API 取得中證500 成分股清單（BK0701，翻頁抓取）。</summary>
public class EastmoneyCsi500ConstituentProvider : IConstituentProvider
{
    private readonly HttpClient _http;

    public EastmoneyCsi500ConstituentProvider()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0 Safari/537.36");
        _http.DefaultRequestHeaders.Add("Referer", "https://gu.qq.com/");
    }

    public async Task<IReadOnlyList<StockSymbol>> FetchAsync(CancellationToken ct = default)
    {
        const int pageSize = 100;
        var codes = new List<StockSymbol>();

        for (int page = 1; ; page++)
        {
            var url = "https://push2.eastmoney.com/api/qt/clist/get" +
                      $"?pn={page}&pz={pageSize}&po=1&np=1&fltt=2&invt=2&fid=f3" +
                      "&fs=b:BK0701&fields=f12,f14&ut=bd1d9ddb04089700cf9c27f6f7426281";

            var json = await CommonHttp.RetryGetAsync(_http, url, ct: ct);
            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("data");

            if (data.ValueKind == JsonValueKind.Null) break;
            if (!data.TryGetProperty("diff", out var diff)) break;

            int count = 0;
            foreach (var item in diff.EnumerateArray())
            {
                var code = item.GetProperty("f12").GetString() ?? "";
                var name = item.GetProperty("f14").GetString() ?? "";
                if (!string.IsNullOrEmpty(code))
                {
                    codes.Add(new StockSymbol(code, name));
                    count++;
                }
            }

            if (count < pageSize) break;
        }

        return codes;
    }
}
