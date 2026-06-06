using CommunityToolkit.Mvvm.ComponentModel;

namespace Avalonia.Plugin.TDLSharp.Models;

/// <summary>
/// 通用执行历史记录，适用于所有脚本。
/// </summary>
public partial class ExecutionHistoryRecord : ObservableObject
{
    public int Id { get; set; }

    /// <summary>脚本 Id</summary>
    public string ScriptId { get; set; } = string.Empty;

    /// <summary>脚本名称</summary>
    public string ScriptName { get; set; } = string.Empty;

    /// <summary>执行时各参数的 Key=Value（JSON 序列化）</summary>
    public string ParametersJson { get; set; } = "{}";

    /// <summary>可读的参数摘要</summary>
    [ObservableProperty] private string _parameterSummary = string.Empty;

    /// <summary>执行时间</summary>
    [ObservableProperty] private DateTime _executedAt;

    /// <summary>执行耗时</summary>
    [ObservableProperty] private TimeSpan _duration;

    /// <summary>执行状态：成功 / 失败 / 已取消</summary>
    [ObservableProperty] private string _status = string.Empty;

    /// <summary>错误信息（如有）</summary>
    [ObservableProperty] private string? _errorMessage;

    public string StatusIcon => Status switch
    {
        "成功" => "✅",
        "部分完成" => "⚠️",
        "已取消" => "🛑",
        "失败" => "❌",
        _ => "⏳"
    };

    /// <summary>执行耗时可读文本</summary>
    public string DurationText => Duration.TotalSeconds < 1
        ? $"{Duration.TotalMilliseconds:F0}ms"
        : Duration.TotalMinutes < 1
            ? $"{Duration.TotalSeconds:F1}s"
            : $"{Duration.Minutes}m {Duration.Seconds}s";
}
