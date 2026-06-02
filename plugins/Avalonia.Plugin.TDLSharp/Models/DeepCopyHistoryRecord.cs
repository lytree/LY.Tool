using CommunityToolkit.Mvvm.ComponentModel;

namespace Avalonia.Plugin.TDLSharp.Models;

public partial class DeepCopyHistoryRecord : ObservableObject
{
    public int Id { get; set; }

    [ObservableProperty] private string _sourceChannel = string.Empty;
    [ObservableProperty] private DateTime _executedAt;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private int _forwardedCount;
    [ObservableProperty] private int _skippedCount;
    [ObservableProperty] private string? _errorMessage;

    public string StatusIcon => Status switch
    {
        "成功" => "✅",
        "部分完成" => "⚠️",
        "失败" => "❌",
        _ => "⏳"
    };
}
