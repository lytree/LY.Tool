using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.ProDataGrid.Models;
using Avalonia.Plugin.ProDataGrid.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace Avalonia.Plugin.ProDataGrid.ViewModels;

[NavigationItem("KeyFilteringGrouping")]
[Menu("NAV_FilteringGrouping", "KeyFilteringGrouping", "NAV_ProDataGrid")]
[ViewMap(typeof(FilteringGroupingDemo))]
public partial class FilteringGroupingDemoViewModel : ObservableObject
{
    private static readonly string[] Titles =
    [
        "设计新版首页UI原型", "修复用户登录超时Bug", "编写支付模块单元测试", "代码审查 PR #128",
        "更新API接口文档", "部署v2.3到预发布环境", "重构权限认证模块", "实现全文搜索功能",
        "优化首页加载性能", "搭建CI/CD自动化流水线", "开发数据导出功能", "修复移动端适配问题",
        "集成第三方支付SDK", "设计数据库ER图", "编写用户操作手册", "实现消息推送服务",
        "优化SQL查询性能", "开发文件上传组件", "配置灰度发布策略", "实现数据备份定时任务",
        "开发用户反馈收集模块", "修复并发数据一致性问题", "设计微服务拆分方案", "实现操作日志审计功能",
        "开发数据大屏可视化", "优化Redis缓存策略", "实现多语言国际化支持", "开发自动化回归测试脚本",
        "设计系统监控告警方案", "实现单点登录集成"
    ];
    private static readonly string[] Assignees =
    [
        "张伟", "王芳", "李娜", "刘洋", "陈静", "杨磊", "赵敏", "黄强",
        "周婷", "吴鹏", "徐明", "孙丽"
    ];
    private static readonly string[] Statuses = ["待开始", "进行中", "审核中", "已完成"];
    private static readonly string[] Priorities = ["低", "中", "高", "紧急"];
    private static readonly string[] Categories =
    [
        "前端开发", "后端开发", "运维部署", "测试验证", "UI设计", "文档编写",
        "数据库", "安全审计", "性能优化", "架构设计"
    ];
    private static readonly string[] Descriptions =
    [
        "需要在本次迭代结束前完成，影响后续多个模块的开发进度",
        "阻塞了其他团队成员的工作，需要优先处理",
        "低优先级维护任务，可安排在空闲时间处理",
        "客户反馈的生产环境问题，需要尽快修复",
        "技术债务清理，建议在本季度内完成",
        "来自产品经理的新功能需求，已通过评审",
        "安全扫描发现的高危漏洞，需要立即修复",
        "性能监控触发的告警，响应时间超过阈值",
        "合规审计要求的必要改动，有截止日期",
        "团队技术分享准备，需要提前准备演示材料"
    ];
    private static readonly Random _random = new();
    private int _nextId = 1;
    private List<TaskItem> _allTasks;

    public ObservableCollection<TaskItem> Tasks { get; }

    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private TaskItem? _selectedTask;

    public ObservableCollection<string> GroupByOptions { get; } = ["无", "状态", "优先级", "分类", "负责人"];

    [ObservableProperty] private string _groupByProperty = "无";

    public FilteringGroupingDemoViewModel()
    {
        _allTasks = GenerateTasks(50);
        Tasks = new ObservableCollection<TaskItem>(_allTasks);
    }

    partial void OnFilterTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnGroupByPropertyChanged(string value)
    {
        ApplyFilter();
    }

    [RelayCommand]
    private void AddTask()
    {
        var task = CreateRandomTask();
        _allTasks.Add(task);
        if (string.IsNullOrWhiteSpace(FilterText) || MatchesFilter(task))
            Tasks.Add(task);
    }

    [RelayCommand]
    private void ResetData()
    {
        _nextId = 1;
        _allTasks = GenerateTasks(50);
        FilterText = string.Empty;
        Tasks.Clear();
        foreach (var t in _allTasks)
            Tasks.Add(t);
    }

    [RelayCommand]
    private void ClearFilter()
    {
        FilterText = string.Empty;
    }

    [RelayCommand]
    private void Save()
    {
    }

    private void ApplyFilter()
    {
        Tasks.Clear();
        var filtered = string.IsNullOrWhiteSpace(FilterText)
            ? _allTasks
            : _allTasks.Where(MatchesFilter);

        if (GroupByProperty != "无")
        {
            var propName = GroupByProperty switch
            {
                "状态" => "Status",
                "优先级" => "Priority",
                "分类" => "Category",
                "负责人" => "Assignee",
                _ => "Status"
            };
            filtered = propName switch
            {
                "Status" => filtered.OrderBy(t => t.Status),
                "Priority" => filtered.OrderBy(t => t.Priority),
                "Category" => filtered.OrderBy(t => t.Category),
                "Assignee" => filtered.OrderBy(t => t.Assignee),
                _ => filtered
            };
        }

        foreach (var t in filtered)
            Tasks.Add(t);
    }

    private bool MatchesFilter(TaskItem task)
    {
        var filter = FilterText.Trim();
        return task.Title.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || task.Assignee.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || task.Status.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || task.Priority.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || task.Category.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private List<TaskItem> GenerateTasks(int count)
    {
        var list = new List<TaskItem>();
        for (int i = 0; i < count; i++)
            list.Add(CreateRandomTask());
        return list;
    }

    private TaskItem CreateRandomTask()
    {
        var status = Statuses[_random.Next(Statuses.Length)];
        var priority = Priorities[_random.Next(Priorities.Length)];
        var progress = status switch
        {
            "已完成" => 100.0,
            "审核中" => Math.Round(_random.NextDouble() * 20 + 80, 1),
            "进行中" => Math.Round(_random.NextDouble() * 70 + 10, 1),
            "待开始" => 0.0,
            _ => Math.Round(_random.NextDouble() * 100, 1)
        };
        var dueDate = priority switch
        {
            "紧急" => DateTime.Now.AddDays(_random.Next(1, 5)),
            "高" => DateTime.Now.AddDays(_random.Next(3, 14)),
            "中" => DateTime.Now.AddDays(_random.Next(7, 30)),
            "低" => DateTime.Now.AddDays(_random.Next(14, 60)),
            _ => DateTime.Now.AddDays(_random.Next(7, 30))
        };

        return new TaskItem(
            _nextId++,
            Titles[_random.Next(Titles.Length)],
            Assignees[_random.Next(Assignees.Length)],
            status,
            priority,
            progress,
            dueDate,
            Categories[_random.Next(Categories.Length)],
            Descriptions[_random.Next(Descriptions.Length)]
        );
    }
}
