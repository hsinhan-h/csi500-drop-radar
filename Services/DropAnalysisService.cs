using Csi500DropRadar.Models;
using Csi500DropRadar.Services.Indices;

namespace Csi500DropRadar.Services;

/// <summary>
/// 泛化的跌幅分析服務。所有指數特定邏輯（時區、資料源、成分股清單）
/// 均由 <see cref="IMarketIndexModule"/> 封裝，此類別不感知任何具體市場。
/// </summary>
public class DropAnalysisService
{
    private const int MaxWorkers     = 10;
    private const double SleepPerReq = 0.2; // 秒

    private readonly TaskManagerService _taskManager;
    private readonly IndexRegistry      _registry;

    public DropAnalysisService(TaskManagerService taskManager, IndexRegistry registry)
    {
        _taskManager = taskManager;
        _registry    = registry;
    }

    public async Task RunAsync(string taskId, string indexId, string period, int topN)
    {
        _taskManager.SetRunning(taskId);
        void Progress(string msg, int? pct = null) =>
            _taskManager.UpdateProgress(taskId, msg, pct);

        try
        {
            var module     = _registry.Get(indexId);
            var descriptor = module.Descriptor;

            // Step 1：計算交易日區間
            var endDate   = module.Clock.ResolveLatestTradingDay();
            var startDate = period == "week" ? endDate.AddDays(-7) : endDate.AddMonths(-1);

            // Step 2：取得成分股清單
            Progress($"正在取得 {descriptor.DisplayNameZh} 成分股清單...");
            var symbols = await module.Constituents.FetchAsync();
            var total   = symbols.Count;
            Progress($"成分股清單共 {total} 檔，開始抓取行情，請耐心等候...");

            // Step 3：並行抓取個股行情
            var results   = new System.Collections.Concurrent.ConcurrentBag<StockRecord>();
            var doneCount = 0;
            var semaphore = new SemaphoreSlim(MaxWorkers);

            var tasks = symbols.Select(async sym =>
            {
                await semaphore.WaitAsync();
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(SleepPerReq));
                    var record = await module.Quotes.FetchAsync(sym, startDate, endDate);
                    if (record != null) results.Add(record);
                }
                finally
                {
                    semaphore.Release();
                    var done = Interlocked.Increment(ref doneCount);
                    Progress(
                        $"抓取進度：{done}/{total}，成功 {results.Count} 檔...",
                        (int)Math.Round((double)done / total * 100));
                }
            });

            await Task.WhenAll(tasks);

            // Step 4：排序並儲存
            Progress("排序整理結果中...");
            var sorted     = results.OrderBy(r => r.PctChange).ToList();
            var topLosers  = sorted.Take(topN).ToList();

            _taskManager.SetDone(taskId, new TaskResult
            {
                IndexId     = descriptor.Id,
                Period      = period,
                Currency    = descriptor.Currency,
                StartDate   = startDate.ToString("yyyyMMdd"),
                EndDate     = endDate.ToString("yyyyMMdd"),
                Fetched     = sorted.Count,
                Total       = total,
                GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                TopLosers   = topLosers,
                AllData     = sorted,
            });
        }
        catch (Exception ex)
        {
            _taskManager.SetError(taskId, ex.Message);
        }
    }
}
