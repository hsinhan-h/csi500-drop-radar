using ClosedXML.Excel;
using Csi500DropRadar.Models;
using Csi500DropRadar.Services;
using Csi500DropRadar.Services.Indices;
using Csi500DropRadar.Services.Localization;
using Microsoft.AspNetCore.Mvc;

namespace Csi500DropRadar.Controllers;

[ApiController]
public class ApiController : ControllerBase
{
    private readonly TaskManagerService  _taskManager;
    private readonly DropAnalysisService _dropService;
    private readonly IndexRegistry       _registry;

    public ApiController(
        TaskManagerService taskManager,
        DropAnalysisService dropService,
        IndexRegistry registry)
    {
        _taskManager = taskManager;
        _dropService  = dropService;
        _registry     = registry;
    }

    // GET /api/indices — 回傳所有可用指數的 descriptor
    [HttpGet("/api/indices")]
    public IActionResult Indices() =>
        Ok(_registry.All().Select(m => new
        {
            id                       = m.Descriptor.Id,
            display_name_zh          = m.Descriptor.DisplayNameZh,
            display_name_en          = m.Descriptor.DisplayNameEn,
            currency                 = m.Descriptor.Currency,
            expected_constituent_count = m.Descriptor.ExpectedConstituentCount,
            estimated_time_zh        = m.Descriptor.EstimatedTimeZh,
            estimated_time_en        = m.Descriptor.EstimatedTimeEn,
        }));

    // POST /api/start
    [HttpPost("/api/start")]
    public IActionResult Start([FromBody] StartRequest req)
    {
        var indexId = req.Index ?? "csi500";
        // 驗證 indexId 合法（讓 registry 拋例外時轉換為 400）
        try { _registry.Get(indexId); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }

        var taskId = _taskManager.CreateTask();
        _ = _dropService.RunAsync(taskId, indexId, req.Period ?? "month", req.TopN);
        return Ok(new { task_id = taskId });
    }

    // GET /api/status/{taskId}
    [HttpGet("/api/status/{taskId}")]
    public IActionResult Status(string taskId)
    {
        var task = _taskManager.Get(taskId);
        if (task == null) return NotFound(new { error = "找不到任務" });

        return Ok(new
        {
            status   = task.Status.ToString().ToLower(),
            progress = task.Progress,
            pct      = task.Pct,
            result   = task.Status == JobStatus.Done ? BuildResultDto(task.Result!) : null,
            error    = task.Error,
        });
    }

    // GET /api/download/{taskId}?top_n=10&lang=zh-TW
    [HttpGet("/api/download/{taskId}")]
    public IActionResult Download(
        string taskId,
        [FromQuery(Name = "top_n")] int topN = 10,
        [FromQuery] string? lang = null)
    {
        var task = _taskManager.Get(taskId);
        if (task == null || task.Status != JobStatus.Done)
            return BadRequest(new { error = "資料尚未準備好" });

        // 優先使用 query lang，其次 Accept-Language header
        var locale = lang
            ?? Request.Headers["Accept-Language"].FirstOrDefault()
            ?? "zh-TW";

        var result    = task.Result!;
        var topLosers = result.AllData.Take(topN).ToList();
        var module    = _registry.Get(result.IndexId);
        var nameZh    = module.Descriptor.DisplayNameZh;
        var periodLbl = LocalizationHelper.PeriodLabel(result.Period, locale);

        using var wb = new XLWorkbook();

        var sheetTop = locale.StartsWith("zh")
            ? $"跌幅前{topN}（{periodLbl}）"
            : $"Top {topN} ({periodLbl})";
        var sheetAll = LocalizationHelper.Get("sheet.all", locale);

        WriteSheet(wb, sheetTop, topLosers, withIndex: true,  locale: locale);
        WriteSheet(wb, sheetAll, result.AllData, withIndex: false, locale: locale);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var filename = $"{nameZh}跌幅分析_{periodLbl}_{result.GeneratedAt[..10]}.xlsx";
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            filename);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static void WriteSheet(
        XLWorkbook wb,
        string sheetName,
        List<StockRecord> records,
        bool withIndex,
        string locale)
    {
        string L(string key) => LocalizationHelper.Get(key, locale);

        var ws  = wb.Worksheets.Add(sheetName);
        int col = 1;
        if (withIndex) ws.Cell(1, col++).Value = L("col.rank");
        ws.Cell(1, col++).Value = L("col.code");
        ws.Cell(1, col++).Value = L("col.name");
        ws.Cell(1, col++).Value = L("col.start_close");
        ws.Cell(1, col++).Value = L("col.end_close");
        ws.Cell(1, col++).Value = L("col.pct_change");
        ws.Cell(1, col++).Value = L("col.start_date");
        ws.Cell(1, col).Value   = L("col.end_date");

        for (int i = 0; i < records.Count; i++)
        {
            var r = records[i];
            int row = i + 2, c = 1;
            if (withIndex) ws.Cell(row, c++).Value = i + 1;
            ws.Cell(row, c++).Value = r.Code;
            ws.Cell(row, c++).Value = r.Name;
            ws.Cell(row, c++).Value = r.StartClose;
            ws.Cell(row, c++).Value = r.EndClose;
            ws.Cell(row, c++).Value = r.PctChange;
            ws.Cell(row, c++).Value = r.StartDate;
            ws.Cell(row, c).Value   = r.EndDate;
        }
    }

    private static object BuildResultDto(TaskResult r) => new
    {
        index_id     = r.IndexId,
        period       = r.Period,
        currency     = r.Currency,
        start_date   = r.StartDate,
        end_date     = r.EndDate,
        fetched      = r.Fetched,
        total        = r.Total,
        generated_at = r.GeneratedAt,
        top_losers   = r.TopLosers.Select((s, i) => ToDto(s, i + 1)),
        all_data     = r.AllData.Select(s => ToDto(s, 0)),
    };

    private static object ToDto(StockRecord s, int rank) => new
    {
        rank,
        code        = s.Code,
        name        = s.Name,
        start_close = s.StartClose,
        end_close   = s.EndClose,
        pct_change  = s.PctChange,
        start_date  = s.StartDate,
        end_date    = s.EndDate,
    };
}

public class StartRequest
{
    public string? Index  { get; set; }   // "csi500" | "sox" | "ndx"（預設 csi500）
    public string? Period { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("top_n")]
    public int TopN { get; set; } = 10;
}
