using System.Collections.Concurrent;
using Csi500DropRadar.Models;

namespace Csi500DropRadar.Services;

public class TaskManagerService
{
    private readonly ConcurrentDictionary<string, TaskState> _tasks = new();

    public string CreateTask()
    {
        var taskId = $"task_{DateTime.Now:HHmmssfffff}";
        _tasks[taskId] = new TaskState { Status = JobStatus.Pending };
        return taskId;
    }

    public TaskState? Get(string taskId) =>
        _tasks.TryGetValue(taskId, out var t) ? t : null;

    public void UpdateProgress(string taskId, string message, int? pct = null)
    {
        if (_tasks.TryGetValue(taskId, out var t))
        {
            t.Progress = message;
            if (pct.HasValue) t.Pct = pct.Value;
        }
    }

    public void SetRunning(string taskId)
    {
        if (_tasks.TryGetValue(taskId, out var t))
            t.Status = JobStatus.Running;
    }

    public void SetDone(string taskId, TaskResult result)
    {
        if (_tasks.TryGetValue(taskId, out var t))
        {
            t.Status = JobStatus.Done;
            t.Result = result;
            t.Pct = 100;
        }
    }

    public void SetError(string taskId, string error)
    {
        if (_tasks.TryGetValue(taskId, out var t))
        {
            t.Status = JobStatus.Error;
            t.Error = error;
        }
    }
}
