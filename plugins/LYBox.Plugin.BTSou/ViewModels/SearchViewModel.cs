using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using LYBox.Plugin.BTSou.Models;
using LYBox.Plugin.BTSou.Services;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LYBox.Plugin.BTSou.ViewModels;

/// <summary>
/// BTSOU 资源搜索页（还原原程序主窗口类 a 的搜索能力）：
/// 关键词搜索、来源库下拉（Search All + 各库）、暂停/继续加载、
/// 结果表格（标题/大小/时间/来源）、复制磁链、导出种子镜像、迅雷一键下载、热搜关键词。
/// </summary>
[NavigationItem("BTSou_Search")]
[Menu("NAV_BTSou_Search", "BTSou_Search", ParentKey = "NAV_BTSou", Order = 1)]
[ViewMap(typeof(Pages.SearchPage))]
public partial class SearchViewModel : ViewModelBase
{
    private readonly BTSouSearchService _search;
    private CancellationTokenSource? _cts;
    private bool _suspend;
    private bool _isSearchingFlag;

    public ObservableCollection<string> SourceLibraries { get; } = ["Search All"];
    public ObservableCollection<SearchResultItem> Results { get; } = [];
    public ObservableCollection<string> SearchHistory { get; } = [];
    public ObservableCollection<string> HotWords { get; } = [];

    [ObservableProperty] private string _keyword = "";
    [ObservableProperty] private string _selectedSource = "Search All";
    [ObservableProperty] private string _searchButtonText = "搜索 [Enter]";
    [ObservableProperty] private string _statusText = "请输入关键词开始搜索";
    [ObservableProperty] private string _poolVersion = "";
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private SearchResultItem? _selectedResult;

    /// <summary>生成器要求公共无参构造函数，服务经静态单例访问。</summary>
    public SearchViewModel()
    {
        _search = BTSouSearchService.Current;
        // 异步初始化（不阻塞 UI）：加载资源池、来源库、热搜
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            await OnActivatedAsync();
        }
        catch
        {
            StatusText = "初始化失败，请检查网络后重试";
        }
    }

    /// <summary>页面激活时加载资源池、热搜与搜索记录</summary>
    public async Task OnActivatedAsync()
    {
        if (_search.ResPoolRaw is null)
        {
            try
            {
                // 还原原程序：优先读本地缓存 ResPool.ryx，无则下载并缓存
                var cachePath = System.IO.Path.Combine(
                    AppContext.BaseDirectory, "ResPool.ryx");
                await _search.LoadResourcePoolAsync(localCachePath: cachePath);
                PoolVersion = $"资源池版本: {_search.Version ?? "未知"}";
                StatusText = "请输入关键词开始搜索";
            }
            catch
            {
                StatusText = "资源池加载失败（网络不可用），可使用本地缓存";
            }
        }
        // 来源库下拉（还原原程序 an 下拉：Search All + 各库名）
        if (SourceLibraries.Count == 1)
        {
            foreach (var lib in _search.SourceLibraries.Select(l => l[0]).Where(x => !string.IsNullOrEmpty(x)))
            {
                if (!SourceLibraries.Contains(lib))
                    SourceLibraries.Add(lib);
            }
        }
        // 热搜关键词（还原原程序 s_1：点击热搜即搜索）
        foreach (var w in _search.HotWords.Where(w => !string.IsNullOrEmpty(w)))
        {
            if (!HotWords.Contains(w))
                HotWords.Add(w);
        }
    }

    // ==================== 搜索（还原 ah_1） ====================

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (_suspend)
        {
            // 继续加载
            _suspend = false;
            SearchButtonText = "暂停加载";
            await RunSearchCoreAsync(_cts?.Token ?? CancellationToken.None);
            return;
        }
        if (SearchButtonText == "暂停加载")
        {
            // 暂停
            _suspend = true;
            SearchButtonText = "继续加载";
            _cts?.Cancel();
            StatusText = $"共为您找到 {Results.Count} 条资源：";
            return;
        }

        if (string.IsNullOrWhiteSpace(Keyword) || Keyword == "请输入关键词")
        {
            StatusText = "请输入关键词再点击搜索！";
            return;
        }
        if (_search.ContainsBlockedWord(Keyword))
        {
            StatusText = "请勿搜索非法关键词！";
            return;
        }

        // 记录搜索历史（还原 aq 下拉记录，最多 10 条）
        if (!SearchHistory.Contains(Keyword))
        {
            SearchHistory.Insert(0, Keyword);
            while (SearchHistory.Count > 10) SearchHistory.RemoveAt(SearchHistory.Count - 1);
        }

        Results.Clear();
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        SearchButtonText = "暂停加载";
        IsSearching = true;
        _isSearchingFlag = true;
        StatusText = "正在为您搜索资源，请稍等...";
        await RunSearchCoreAsync(_cts.Token);
    }

    private async Task RunSearchCoreAsync(CancellationToken ct)
    {
        try
        {
            int count = 0;
            await _search.SearchAsync(Keyword, SelectedSource == "Search All" ? null : SelectedSource,
                maxPerSource: 60,
                onResult: (item, token) =>
                {
                    // 去重：同磁链/同标题只显示一条（还原原程序 h.ag 去重开关的默认行为）
                    if (Results.Any(r => r.Link == item.Link || r.Title == item.Title))
                        return Task.CompletedTask;
                    Dispatcher.UIThread.Post(() => Results.Add(item));
                    count++;
                    return Task.CompletedTask;
                }, ct);
            if (_isSearchingFlag)
            {
                StatusText = $"共为您找到 {count} 条资源：";
                SearchButtonText = "搜索 [Enter]";
                IsSearching = false;
                _isSearchingFlag = false;
            }
        }
        catch (OperationCanceledException)
        {
            // 用户暂停，保持"继续加载"状态
        }
        catch (Exception)
        {
            StatusText = "搜索出错，请稍后再试";
            SearchButtonText = "搜索 [Enter]";
            IsSearching = false;
            _isSearchingFlag = false;
        }
    }

    /// <summary>搜索下一个来源库（还原 ai_1：切换下拉索引并再次搜索）</summary>
    [RelayCommand]
    private void NextSource()
    {
        if (SourceLibraries.Count <= 1)
        {
            StatusText = "无可供搜索的资源库！";
            return;
        }
        var idx = SourceLibraries.IndexOf(SelectedSource);
        SelectedSource = idx >= SourceLibraries.Count - 1 ? SourceLibraries[1] : SourceLibraries[idx + 1];
        StatusText = $"已切换到资源库: {SelectedSource}";
    }

    [RelayCommand]
    private void HotWordSearch(string? word)
    {
        if (string.IsNullOrEmpty(word)) return;
        Keyword = word;
        _ = SearchAsync();
    }

    // ==================== 结果操作（还原右键菜单） ====================

    /// <summary>一键迅雷下载（还原 c_3）</summary>
    [RelayCommand]
    private void DownloadWithThunder(SearchResultItem? item)
    {
        if (item is null) return;
        StatusText = BTSouSearchService.DownloadWithThunder(item.Link)
            ? "成功打开迅雷"
            : "无法启动迅雷，请使用手动下载！";
    }

    /// <summary>复制磁链（还原 ao_1）</summary>
    [RelayCommand]
    private async Task CopyLinkAsync(SearchResultItem? item)
    {
        if (item is null) return;
        try
        {
            var topLevel = Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
            var clipboard = topLevel?.Clipboard;
            if (clipboard is not null)
            {
                await clipboard.SetTextAsync(item.Link);
                StatusText = "磁力链接已复制";
            }
        }
        catch { StatusText = "复制失败"; }
    }

    /// <summary>复制种子镜像 URL（还原原程序 torrent 导出）</summary>
    [RelayCommand]
    private async Task CopyTorrentUrlAsync(SearchResultItem? item)
    {
        if (item is null || item.TorrentMirrorUrl is null) return;
        try
        {
            var topLevel = Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
            var clipboard = topLevel?.Clipboard;
            if (clipboard is not null)
            {
                await clipboard.SetTextAsync(item.TorrentMirrorUrl);
                StatusText = "种子镜像地址已复制";
            }
        }
        catch { StatusText = "复制失败"; }
    }

    /// <summary>清空搜索结果</summary>
    [RelayCommand]
    private void ClearResults()
    {
        _cts?.Cancel();
        Results.Clear();
        StatusText = "请输入关键词开始搜索";
        SearchButtonText = "搜索 [Enter]";
        IsSearching = false;
        _isSearchingFlag = false;
        _suspend = false;
    }

    public override void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        base.Dispose();
    }
}
