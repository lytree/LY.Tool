using System.Collections.Concurrent;
using LYBox.Plugin.Shared.Services;

namespace LYBox.UrsaWindow.Services;

public sealed class TaskRegistry : ITaskRegistry
{
    private readonly ConcurrentDictionary<string, TaskToken> _tasks = new();

    public bool HasRunningTasks => !_tasks.IsEmpty;

    public event EventHandler? RunningTasksChanged;

    public TaskToken Register(string taskName, string pluginId)
    {
        var token = new TaskToken(taskName, pluginId, this);
        _tasks[token.TaskId] = token;
        RunningTasksChanged?.Invoke(this, EventArgs.Empty);
        return token;
    }

    public void Unregister(TaskToken token)
    {
        _tasks.TryRemove(token.TaskId, out _);
        RunningTasksChanged?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<RunningTask> GetRunningTasks()
    {
        return _tasks.Values.Select(t => new RunningTask
        {
            TaskId = t.TaskId,
            TaskName = t.TaskName,
            PluginId = t.PluginId,
            StartTime = t.StartTime,
            IsCompleted = t.IsCompleted
        }).ToList().AsReadOnly();
    }
}
