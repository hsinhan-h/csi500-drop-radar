using System.Text.Json;
using System.Text.RegularExpressions;
using Csi500DropRadar.Models;

namespace Csi500DropRadar.Services;

public class StockFetchService
{
    private const int MaxWorkers = 10;
    private const double SleepPerRequest = 0.2; // 秒

    private readonly HttpClient _http;
    private readonly TaskManagerService _taskManager;

    public StockFetchService(HttpClient http, TaskManagerService taskManager)
    {
        _http = http;
        _taskManager = taskManager;
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0 Safari/537.36");
        _http.DefaultRequestHeaders.Add("Referer", "https://gu.qq.com/");
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task RunAsync(string taskId, string period, int topN)
    {
        _taskManager.SetRunning(taskId);
        void Progress(string msg, int? pct = null) => _taskManager.UpdateProgress(taskId, msg, pct);

        try
        {
            // 以北京時間為基準：15:00（收盤）後才算當日資料完整
            var chinaTimeZone = GetChinaTimeZone();
            var chinaNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, chinaTimeZone);
            var baseDate = chinaNow.TimeOfDay < TimeSpan.FromHours(15)
                ? chinaNow.Date.AddDays(-1)   // 盤前 / 盤中，推回前一天
                : chinaNow.Date;               // 盤後，使用今天

            var endDate = TradingCalendar.GetLatestTradingDay(baseDate);
            var startDate = period == "week" ? endDate.AddDays(-7) : endDate.AddMonths(-1);
            var startStr = startDate.ToString("yyyy-MM-dd");
            var endStr = endDate.ToString("yyyy-MM-dd");

            // Step 1: 取得成分股清單
            Progress("正在取得中證500成分股清單...");
            var (codes, namesMap) = await FetchConstituentListAsync();
            var total = codes.Count;
            Progress($"成分股清單共 {total} 檔，開始抓取行情，請耐心等候...");

            // Step 2: 並行抓取個股行情
            var results = new System.Collections.Concurrent.ConcurrentBag<StockRecord>();
            var doneCount = 0;
            var semaphore = new SemaphoreSlim(MaxWorkers);

            var tasks = codes.Select(async code =>
            {
                await semaphore.WaitAsync();
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(SleepPerRequest));
                    var record = await FetchOneAsync(code, namesMap.GetValueOrDefault(code, code), startStr, endStr);
                    if (record != null) results.Add(record);
                }
                finally
                {
                    semaphore.Release();
                    var done = Interlocked.Increment(ref doneCount);
                    Progress($"抓取進度：{done}/{total}，成功 {results.Count} 檔...",
                             (int)Math.Round((double)done / total * 100));
                }
            });

            await Task.WhenAll(tasks);

            // Step 3: 排序
            Progress("排序整理結果中...");
            var sorted = results.OrderBy(r => r.PctChange).ToList();
            var topLosers = sorted.Take(topN).ToList();

            _taskManager.SetDone(taskId, new TaskResult
            {
                PeriodLabel = period == "week" ? "近一週" : "近一個月",
                StartDate = startDate.ToString("yyyyMMdd"),
                EndDate = endDate.ToString("yyyyMMdd"),
                Fetched = sorted.Count,
                Total = total,
                GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                TopLosers = topLosers,
                AllData = sorted,
            });
        }
        catch (Exception ex)
        {
            _taskManager.SetError(taskId, ex.Message);
        }
    }

    // ── 成分股清單：東方財富（翻頁抓取，每頁100筆）──────────────────
    private async Task<(List<string> codes, Dictionary<string, string> names)> FetchConstituentListAsync()
    {
        const int pageSize = 100;
        var codes = new List<string>();
        var names = new Dictionary<string, string>();

        for (int page = 1; ; page++)
        {
            var url = "https://push2.eastmoney.com/api/qt/clist/get" +
                      $"?pn={page}&pz={pageSize}&po=1&np=1&fltt=2&invt=2&fid=f3" +
                      "&fs=b:BK0701&fields=f12,f14&ut=bd1d9ddb04089700cf9c27f6f7426281";

            var json = await RetryGetAsync(url);
            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("data");

            // data 為 null 代表沒有更多資料
            if (data.ValueKind == JsonValueKind.Null) break;
            if (!data.TryGetProperty("diff", out var diff)) break;

            int count = 0;
            foreach (var item in diff.EnumerateArray())
            {
                var code = item.GetProperty("f12").GetString() ?? "";
                var name = item.GetProperty("f14").GetString() ?? "";
                if (!string.IsNullOrEmpty(code))
                {
                    codes.Add(code);
                    names[code] = name;
                    count++;
                }
            }

            // 沒有更多資料就停止
            if (count < pageSize) break;
        }

        return (codes, names);
    }

    // ── 個股歷史行情：騰訊財經 ────────────────────────────────────────
    private async Task<StockRecord?> FetchOneAsync(string code, string name, string startDate, string endDate)
    {
        var prefix = code.StartsWith("6") ? "sh" : "sz";
        var r = new Random().NextDouble();
        var url = $"https://web.ifzq.gtimg.cn/appstock/app/fqkline/get" +
                  $"?_var=kline_dayqfq&param={prefix}{code},day,{startDate},{endDate},500,qfq&r={r:F6}";

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

                // 路徑：data → {prefix}{code} → qfqday 或 day
                var dataNode = root.GetProperty("data").GetProperty($"{prefix}{code}");
                JsonElement klines;
                if (!dataNode.TryGetProperty("qfqday", out klines) &&
                    !dataNode.TryGetProperty("day", out klines))
                    return null;

                var arr = klines.EnumerateArray().ToList();
                if (arr.Count < 2) return null;

                var first = arr[0].EnumerateArray().ToList();
                var last = arr[^1].EnumerateArray().ToList();

                var startClose = double.Parse(first[2].GetString() ?? "0");
                var endClose = double.Parse(last[2].GetString() ?? "0");
                if (startClose <= 0) return null;

                return new StockRecord
                {
                    Code = code,
                    Name = name,
                    StartClose = Math.Round(startClose, 2),
                    EndClose = Math.Round(endClose, 2),
                    PctChange = Math.Round((endClose - startClose) / startClose * 100, 2),
                    StartDate = first[0].GetString()?[..10] ?? "",
                    EndDate = last[0].GetString()?[..10] ?? "",
                };
            }
            catch (Exception ex)
            {
                var wait = 2 * (attempt + 1);
                Console.WriteLine($"[WARN] {code} 第{attempt + 1}次失敗，等待{wait}s: {ex.Message}");
                await Task.Delay(wait * 1000);
            }
        }

        return null;
    }

    // Windows："China Standard Time"；Linux/Docker："Asia/Shanghai"
    private static TimeZoneInfo GetChinaTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("China Standard Time"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai"); }
    }

    private async Task<string> RetryGetAsync(string url)
    {
        for (int i = 0; i < 3; i++)
        {
            try { return await _http.GetStringAsync(url); }
            catch { if (i == 2) throw; await Task.Delay(2000); }
        }
        throw new Exception("retry failed");
    }
}
