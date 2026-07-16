using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Models;
using LYBox.Plugin.Shared.Services;
using LYBox.Plugin.Shared.ViewModels;
using LYBox.UrsaWindow.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace LYBox.UrsaWindow.ViewModels;

public partial class IntroductionDemoViewModel : ObservableObject
{
 

    [ObservableProperty] private double _ratingValue = 3.5;
    [ObservableProperty] private int _sliderValue = 50;
    [ObservableProperty] private IPAddress? _ipAddress = new IPAddress(new byte[] { 192, 168, 1, 1 });
    [ObservableProperty] private double _lowerValue = 20;
    [ObservableProperty] private double _upperValue = 80;
    [ObservableProperty] private DateTime _startDate = DateTime.Today;
    [ObservableProperty] private DateTime _endDate = DateTime.Today.AddDays(7);
    [ObservableProperty] private DateTime _dateTime = DateTime.Now;
    [ObservableProperty] private TimeSpan _startTime = new TimeSpan(9, 0, 0);
    [ObservableProperty] private TimeSpan _endTime = new TimeSpan(17, 0, 0);

    private readonly ILocalizationService? _localizationService;
    private List<ToolGroup> _allGroups = [];

    /// <summary>
    /// 按目录分组的工具列表（已过滤），替换整组引用触发单次 PropertyChanged 通知，
    /// 避免 ObservableCollection 逐项 Add 导致的多次布局刷新。
    /// </summary>
    [ObservableProperty] private IReadOnlyList<ToolGroup> _filteredGroups = [];

    [ObservableProperty] private string? _searchText;
    [ObservableProperty] private bool _hasNoResults;

    public IntroductionDemoViewModel()
    {
        _localizationService = ServiceLocator.TryGetService<ILocalizationService>(out var loc) ? loc : null;
        LoadToolGroups();
        WeakReferenceMessenger.Default.Register<IntroductionDemoViewModel, string, string>(this, "SearchChanged", OnSearchChanged);
    }

    private void OnSearchChanged(IntroductionDemoViewModel recipient, string message)
    {
        SearchText = message;
    }

    partial void OnSearchTextChanged(string? value)
    {
        ApplyFilter(value);
    }

    /// <summary>
    /// 从菜单配置服务加载所有工具，按父级目录分组
    /// </summary>
    private void LoadToolGroups()
    {
        if (!ServiceLocator.TryGetService<IMenuConfigurationService>(out var menuConfig) || menuConfig is null)
            return;

        var menu = menuConfig.GetMenuStructure();
        _allGroups.Clear();
        var navCmd = NavigateCommand;

        foreach (var parent in menu.MenuItems)
        {
            if (parent.IsSeparator || string.IsNullOrEmpty(parent.Key)) continue;

            var group = new ToolGroup
            {
                GroupName = ResolveHeader(parent.MenuHeader) ?? parent.Key ?? string.Empty,
                GroupKey = parent.Key ?? string.Empty
            };

            foreach (var child in parent.Children)
            {
                if (child.IsSeparator || string.IsNullOrEmpty(child.Key)) continue;
                group.Items.Add(new ToolItem
                {
                    Name = ResolveHeader(child.MenuHeader) ?? child.Key ?? string.Empty,
                    Key = child.Key!,
                    Status = child.Status,
                    NavigateCommand = navCmd
                });
            }

            if (group.Items.Count > 0)
                _allGroups.Add(group);
        }

        ApplyFilter(SearchText);
    }

    private string? ResolveHeader(string? header)
    {
        if (string.IsNullOrEmpty(header)) return header;
        return _localizationService?.GetString(header) ?? header;
    }

    private void ApplyFilter(string? search)
    {
        List<ToolGroup> result;
        if (string.IsNullOrWhiteSpace(search))
        {
            result = _allGroups;
        }
        else
        {
            var keyword = search.Trim();
            result = new List<ToolGroup>();
            foreach (var group in _allGroups)
            {
                var groupMatched = group.GroupName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                                    || group.GroupKey.Contains(keyword, StringComparison.OrdinalIgnoreCase);

                if (groupMatched)
                {
                    result.Add(group);
                    continue;
                }

                List<ToolItem>? matched = null;
                foreach (var item in group.Items)
                {
                    if (item.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                        || item.Key.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    {
                        (matched ??= new List<ToolItem>()).Add(item);
                    }
                }

                if (matched is { Count: > 0 })
                {
                    var filteredGroup = new ToolGroup
                    {
                        GroupName = group.GroupName,
                        GroupKey = group.GroupKey
                    };
                    foreach (var m in matched)
                        filteredGroup.Items.Add(m);
                    result.Add(filteredGroup);
                }
            }
        }
        FilteredGroups = result;
        HasNoResults = result.Count == 0;
    }

    [RelayCommand]
    private void Navigate(string key)
    {
        WeakReferenceMessenger.Default.Send(key, "JumpTo");
    }

    public void RefreshGroups()
    {
        LoadToolGroups();
    }
}

/// <summary>
/// 工具分组（对应一个插件目录）
/// </summary>
public sealed class ToolGroup
{
    public string GroupName { get; set; } = string.Empty;
    public string GroupKey { get; set; } = string.Empty;
    public ObservableCollection<ToolItem> Items { get; set; } = [];
}

/// <summary>
/// 单个工具项
/// </summary>
public sealed class ToolItem
{
    public string Name { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string? Status { get; set; }
    /// <summary>
    /// 导航命令（由 ViewModel 注入）
    /// </summary>
    public System.Windows.Input.ICommand? NavigateCommand { get; set; }
}
