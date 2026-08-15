using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using LYBox.Plugin.BTSou.Models;
using LYBox.Plugin.BTSou.Services;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LYBox.Plugin.BTSou.ViewModels;

/// <summary>
/// BTSOU 资源搜索页。对应原程序主窗口（类 a）的搜索能力：
/// 关键词搜索 → 来源库匹配 → 生成磁力链接列表。
/// </summary>
[NavigationItem("BTSou_Search")]
[Menu("NAV_BTSou_Search", "BTSou_Search", ParentKey = "NAV_BTSou", Order = 1)]
[ViewMap(typeof(Pages.SearchPage))]
public partial class SearchViewModel : ViewModelBase
{
    private readonly BTSouSearchService _search;
    private readonly BTSouDatabaseService _db;

    public ObservableCollection<SearchResultItem> Results { get; } = [];

    [ObservableProperty] private string _keyword = "";
    [ObservableProperty] private string _statusText = "请输入关键词开始搜索";
    [ObservableProperty] private string _poolVersion = "";
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private string _licenseStatus = "未检测";

    /// <summary>生成器要求公共无参构造函数，服务经静态单例访问。</summary>
    public SearchViewModel()
    {
        _search = BTSouSearchService.Current;
        _db = BTSouDatabaseService.Current;
    }

    /// <summary>页面激活时加载资源池并检测授权状态</summary>
    public async Task OnActivatedAsync()
    {
        if (_search.ResPoolRaw is null)
        {
            try
            {
                await _search.LoadResourcePoolAsync();
                PoolVersion = $"资源池版本: {_search.Version ?? "未知"}";
            }
            catch
            {
                StatusText = "资源池加载失败（网络不可用）";
            }
        }
        try
        {
            var serial = BTSouDatabaseService.GetHardDiskSerial();
            var info = await _db.GetLockInfoAsync(serial);
            LicenseStatus = info.IsLicensed ? $"已授权 (锁码 {info.LockCode})" : "未授权";
        }
        catch
        {
            LicenseStatus = "授权服务器不可达";
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
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

        IsSearching = true;
        Results.Clear();
        StatusText = "正在为您搜索资源，请稍等...";
        await Task.Delay(50);

        // 简化搜索：在资源池行中按关键词匹配（演示逻辑）
        // 原程序为：按来源库构造搜索 URL → 抓取网页 → 解析磁链，此处抽象为本地匹配
        var kw = BTSouSearchService.ToSimplifiedChinese(Keyword);
        var lines = _search.EnumeratePoolLines(_search.ResPoolRaw ?? "").ToList();
        var matched = 0;
        foreach (var line in lines)
        {
            if (matched >= 200) break;
            var cols = line.Split('\t');
            if (cols.Length >= 5)
            {
                var title = cols[0];
                var haystack = (BTSouSearchService.ToSimplifiedChinese(title) + "\t" + BTSouSearchService.ToSimplifiedChinese(line)).ToLowerInvariant();
                if (haystack.Contains(kw.ToLowerInvariant()))
                {
                    Results.Add(new SearchResultItem
                    {
                        Title = cols[0],
                        Size = cols.Length > 1 ? cols[1] : "",
                        UpdateTime = cols.Length > 2 ? cols[2] : "",
                        Source = cols.Length > 3 ? cols[3] : "",
                        Link = cols.Length > 4 ? cols[4] : ""
                    });
                    matched++;
                }
            }
        }
        StatusText = matched > 0 ? $"共为您找到 {matched} 条资源：" : "未找到匹配资源";
        IsSearching = false;
    }

    [RelayCommand]
    private async Task CopyLink(SearchResultItem? item)
    {
        if (item is null) return;
        try
        {
            var topLevel = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
            var clipboard = topLevel?.Clipboard;
            if (clipboard is not null)
            {
                await clipboard.SetTextAsync(item.Link);
                StatusText = "链接已复制";
            }
        }
        catch { StatusText = "复制失败"; }
    }
}
