using ClosedXML.Excel;
using Csi500DropRadar.Models;
using Csi500DropRadar.Services;
using Microsoft.AspNetCore.Mvc;

namespace Csi500DropRadar.Controllers;

[ApiController]
public class ApiController : ControllerBase
{
    private readonly TaskManagerService _taskManager;
    private readonly StockFetchService _fetchService;

    public ApiController(TaskManagerService taskManager, StockFetchService fetchService)
    {
        _taskManager = taskManager;
        _fetchService = fetchService;
    }

    // POST /api/start
    [HttpPost("/api/start")]
    public IActionResult Start([FromBody] StartRequest req)
    {
        var taskId = _taskManager.CreateTask();
        _ = _fetchService.RunAsync(taskId, req.Period ?? "month", req.TopN);
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
            status = task.Status.ToString().ToLower(),
            progress = task.Progress,
            pct = task.Pct,
            result = task.Status == JobStatus.Done ? BuildResultDto(task.Result!) : null,
            error = task.Error,
        });
    }

    // GET /api/download/{taskId}?top_n=10
    [HttpGet("/api/download/{taskId}")]
    public IActionResult Download(string taskId, [FromQuery(Name = "top_n")] int topN = 10)
    {
        var task = _taskManager.Get(taskId);
        if (task == null || task.Status != JobStatus.Done)
            return BadRequest(new { error = "資料尚未準備好" });

        var result = task.Result!;
        var topLosers = result.AllData.Take(topN).ToList();
        using var wb = new XLWorkbook();

        WriteSheet(wb, $"跌幅前{topN}（{result.PeriodLabel}）", topLosers, withIndex: true);
        WriteSheet(wb, "全部成分股", result.AllData, withIndex: false);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var filename = $"中證500跌幅分析_{result.PeriodLabel}_{result.GeneratedAt[..10]}.xlsx";
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            filename);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static void WriteSheet(XLWorkbook wb, string sheetName, List<StockRecord> records, bool withIndex)
    {
        var ws = wb.Worksheets.Add(sheetName);
        int col = 1;
        if (withIndex) ws.Cell(1, col++).Value = "#";
        ws.Cell(1, col++).Value = "代碼";
        ws.Cell(1, col++).Value = "名稱";
        ws.Cell(1, col++).Value = "期初收盤價";
        ws.Cell(1, col++).Value = "最新收盤價";
        ws.Cell(1, col++).Value = "漲跌幅(%)";
        ws.Cell(1, col++).Value = "期初日期";
        ws.Cell(1, col).Value  = "最新日期";

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
            ws.Cell(row, c).Value  = r.EndDate;
        }
    }

    private static object BuildResultDto(TaskResult r) => new
    {
        period_label = r.PeriodLabel,
        start_date   = r.StartDate,
        end_date     = r.EndDate,
        fetched      = r.Fetched,
        total        = r.Total,
        generated_at = r.GeneratedAt,
        top_losers   = r.TopLosers.Select((s, i) => ToDto(s, i + 1)),
        all_data     = r.AllData.Select(s => ToDto(s, 0)),
    };

    private static Dictionary<string, object> ToDto(StockRecord s, int rank) => new()
    {
        ["代碼"]       = s.Code,
        ["名稱"]       = s.Name,
        ["期初收盤價"] = s.StartClose,
        ["最新收盤價"] = s.EndClose,
        ["漲跌幅(%)"]  = s.PctChange,
        ["期初日期"]   = s.StartDate,
        ["最新日期"]   = s.EndDate,
    };
}

public class StartRequest
{
    public string? Period { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("top_n")]
    public int TopN { get; set; } = 10;
}
