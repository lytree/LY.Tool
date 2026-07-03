using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using LYBox.Plugin.Downloader.Models;
using LYBox.Plugin.Downloader.Resources;
using LYBox.Plugin.Downloader.Services;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Services;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace LYBox.Plugin.Downloader.ViewModels;

/// <summary>
/// 所有下载器页面的 ViewModel 基类。
/// 提供日志、执行/取消、TaskScope 注册（主程序退出时可取消）等公共能力。
/// 子类只需实现 ExecuteCoreAsync(CancellationToken) 并自行组装 DownloadOptions。
/// </summary>
public abstract partial class DownloaderViewModelBase : ViewModelBase
{
    /// <summary>本插件的 PluginId，用于 TaskScope 注册</summary>
    protected const string PluginId = "B2C3D4E5-F6A7-8901-BCDE-DOWNLOADER001";

    [ObservableProperty] private ObservableCollection<LogEntry> _logEntries = [];
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _statusText = Strings.Get("STATUS_Ready");
    [ObservableProperty] private double _logMaxHeight = 400;

    private CancellationTokenSource? _cts;

    protected DownloaderViewModelBase()
    {
        WeakReferenceMessenger.Default.Register<DownloaderViewModelBase, WindowSizeChangedMessage>(this, OnWindowSizeChanged);
    }

    private void OnWindowSizeChanged(object recipient, WindowSizeChangedMessage message)
    {
        LogMaxHeight = Math.Max(200, message.Value.Height * 0.5);
    }

    /// <summary>共享的二进制路径配置（来自设置页持久化）</summary>
    protected static BinaryPaths BinaryConfig => DownloadSettingsStore.Current;

    /// <summary>把 BinaryPaths 应用到 DownloadOptions（ffmpeg / mp4decrypt / mkvmerge / 代理 / 日志级别）</summary>
    protected static void ApplyBinaryConfig(DownloadOptions opts)
    {
        var cfg = BinaryConfig;
        opts.FfmpegPath = string.IsNullOrWhiteSpace(cfg.FfmpegPath) ? "ffmpeg" : cfg.FfmpegPath;
        opts.MkvmergePath = string.IsNullOrWhiteSpace(cfg.MkvmergePath) ? "mkvmerge" : cfg.MkvmergePath;
        opts.Mp4DecryptPath = cfg.EffectiveMp4Decrypt();
        opts.ShakaPackagerPath = cfg.EffectiveShaka();
        opts.Proxy = cfg.Proxy;
        opts.UseSystemProxy = cfg.UseSystemProxy;
        opts.LogLevel = cfg.LogLevel;
    }

    [RelayCommand]
    private void ClearLog()
    {
        LogEntries.Clear();
    }

    [RelayCommand]
    private async Task CopyLogEntry(LogEntry entry)
    {
        var topLevel = Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
        var clipboard = topLevel?.Clipboard;
        if (clipboard is not null)
        {
            await clipboard.SetTextAsync(entry.FormattedLine);
        }
    }

    [RelayCommand]
    private async Task ExecuteScript()
    {
        if (IsRunning) return;

        IsRunning = true;
        StatusText = Strings.Get("STATUS_Running", GetDisplayName());
        _cts = new CancellationTokenSource();

        // 注册到 TaskRegistry，主程序退出时可取消
        var taskScope = new TaskScope(GetDisplayName(), PluginId);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, taskScope.Token.CancellationTokenSource.Token);

        try
        {
            await ExecuteCoreAsync(linkedCts.Token);
            StatusText = Strings.Get("STATUS_Completed");
        }
        catch (OperationCanceledException)
        {
            StatusText = Strings.Get("STATUS_Cancelled");
        }
        catch (Exception ex)
        {
            AddLogEntry(new LogEntry { Message = Strings.Get("FMT_ExecuteFailed", ex.Message) });
            StatusText = Strings.Get("STATUS_Failed");
        }
        finally
        {
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
            taskScope.Dispose();
        }
    }

    [RelayCommand]
    private void CancelExecution()
    {
        _cts?.Cancel();
        StatusText = Strings.Get("STATUS_Cancelling");
    }

    /// <summary>页面显示名称（用于状态文本与 TaskScope 标识）</summary>
    protected abstract string GetDisplayName();

    /// <summary>子类实现具体执行逻辑</summary>
    protected abstract Task ExecuteCoreAsync(CancellationToken ct);

    protected DirectUiLogger CreateUiLogger()
        => new(message => AddLogEntry(new LogEntry { Message = message }));

    protected void AddLogEntry(LogEntry entry)
    {
        Dispatcher.UIThread.Post(() =>
        {
            LogEntries.Add(entry);
            if (LogEntries.Count > 1100)
            {
                for (int i = 0; i < 100; i++)
                    LogEntries.RemoveAt(0);
            }
        });
    }

    /// <summary>浏览文件夹选择器（共享实现）</summary>
    protected static async Task<string?> BrowseFolderAsync(string title)
    {
        var topLevel = Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
        if (topLevel == null) return null;

        var result = await topLevel.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });
        return result.Count > 0 ? (result[0].TryGetLocalPath() ?? result[0].Path.ToString()) : null;
    }

    /// <summary>浏览文件选择器（共享实现）</summary>
    protected static async Task<string?> BrowseFileAsync(string title, params string[] extensions)
    {
        var topLevel = Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
        if (topLevel == null) return null;

        var fileTypes = extensions.Length > 0
            ? new[] { new Avalonia.Platform.Storage.FilePickerFileType(title) { Patterns = extensions } }
            : Array.Empty<Avalonia.Platform.Storage.FilePickerFileType>();

        var result = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = fileTypes
        });
        return result.Count > 0 ? (result[0].TryGetLocalPath() ?? result[0].Path.ToString()) : null;
    }

    public override void Dispose()
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
        base.Dispose();
    }
}
