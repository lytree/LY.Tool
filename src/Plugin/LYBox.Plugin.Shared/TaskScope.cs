using LYBox.Plugin.Shared.Services;

namespace LYBox.Plugin.Shared;

/// <summary>
/// 任务注册辅助类。使用 using 语句确保任务结束时（包括异常）自动注销注册信息。
/// 内部持有 TaskToken，可通过 Token 访问任务状态和请求取消。
/// </summary>
public class TaskScope : IDisposable
{
    /// <summary>
    /// 任务 Token。
    /// </summary>
    public TaskToken Token { get; }

    public TaskScope(string taskName, string pluginId)
    {
        ServiceLocator.TryGetService<ITaskRegistry>(out var registry);
        Token = registry?.Register(taskName, pluginId) ?? new TaskToken(taskName, pluginId, null);
    }

    public void Dispose()
    {
        Token.Dispose();
    }
}
