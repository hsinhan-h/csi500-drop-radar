namespace Csi500DropRadar.Models;

public enum JobStatus { Pending, Running, Done, Error }

public class TaskState
{
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public string Progress { get; set; } = "準備中...";
    public int Pct { get; set; } = 0;
    public TaskResult? Result { get; set; }
    public string? Error { get; set; }
}

public class TaskResult
{
    public string PeriodLabel { get; set; } = "";
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public int Fetched { get; set; }
    public int Total { get; set; }
    public string GeneratedAt { get; set; } = "";
    public List<StockRecord> TopLosers { get; set; } = new();
    public List<StockRecord> AllData { get; set; } = new();
}
