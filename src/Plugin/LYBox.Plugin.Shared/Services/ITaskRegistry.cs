namespace LYBox.Plugin.Shared.Services;

/// <summary>
/// 运行任务注册表接口。插件通过此接口注册正在运行的任务，
/// 主程序在退出时检测是否有正在运行的任务并提醒用户。
/// 每个任务通过 TaskToken 跟踪运行状态。
/// </summary>
public interface ITaskRegistry
{
    /// <summary>
    /// 注册一个正在运行的任务，返回任务 Token。
    /// </summary>
    /// <param name="taskName">任务显示名称</param>
    /// <param name="pluginId">所属插件ID</param>
    /// <returns>任务 Token，用于跟踪和管理任务生命周期</returns>
    TaskToken Register(string taskName, string pluginId);

    /// <summary>
    /// 注销一个已结束的任务。
    /// </summary>
    /// <param name="token">任务 Token</param>
    void Unregister(TaskToken token);

    /// <summary>
    /// 获取当前所有正在运行的任务。
    /// </summary>
    IReadOnlyList<RunningTask> GetRunningTasks();

    /// <summary>
    /// 当前是否有正在运行的任务。
    /// </summary>
    bool HasRunningTasks { get; }

    /// <summary>
    /// 正在运行的任务数量变化事件。
    /// </summary>
    event EventHandler? RunningTasksChanged;
}

/// <summary>
/// 任务 Token，用于跟踪和管理单个任务的运行状态。
/// </summary>
public class TaskToken : IDisposable
{
    private readonly ITaskRegistry? _registry;
    private volatile bool _completed;

    /// <summary>
    /// 任务唯一标识。
    /// </summary>
    public string TaskId { get; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 任务显示名称。
    /// </summary>
    public string TaskName { get; }

    /// <summary>
    /// 所属插件ID。
    /// </summary>
    public string PluginId { get; }

    /// <summary>
    /// 任务开始时间。
    /// </summary>
    public DateTime StartTime { get; } = DateTime.UtcNow;

    /// <summary>
    /// 任务关联的取消令牌源，可用于请求取消任务。
    /// </summary>
    public CancellationTokenSource CancellationTokenSource { get; } = new();

    /// <summary>
    /// 任务是否已完成（正常结束或异常结束）。
    /// </summary>
    public bool IsCompleted => _completed;

    /// <summary>
    /// 请求取消任务。
    /// </summary>
    public void Cancel() => CancellationTokenSource.Cancel();

    public TaskToken(string taskName, string pluginId, ITaskRegistry? registry)
    {
        TaskName = taskName;
        PluginId = pluginId;
        _registry = registry;
    }

    /// <summary>
    /// 标记任务完成并从注册表中注销。
    /// </summary>
    public void Complete()
    {
        if (_completed) return;
        _completed = true;
        _registry?.Unregister(this);
    }

    public void Dispose()
    {
        Complete();
        CancellationTokenSource.Dispose();
    }
}

/// <summary>
/// 运行中的任务信息。
/// </summary>
public class RunningTask
{
    public string TaskId { get; init; } = string.Empty;
    public string TaskName { get; init; } = string.Empty;
    public string PluginId { get; init; } = string.Empty;
    public DateTime StartTime { get; init; } = DateTime.UtcNow;
    public bool IsCompleted { get; init; }
}
