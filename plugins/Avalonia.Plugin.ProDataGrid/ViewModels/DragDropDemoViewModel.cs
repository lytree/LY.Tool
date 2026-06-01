using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.ProDataGrid.Models;
using Avalonia.Plugin.ProDataGrid.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace Avalonia.Plugin.ProDataGrid.ViewModels;

[NavigationItem("KeyDragDrop")]
[Menu("NAV_DragDrop", "KeyDragDrop", "NAV_ProDataGrid")]
[ViewMap(typeof(DragDropDemo))]
public partial class DragDropDemoViewModel : ObservableObject
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
    private static readonly Random _random = new();
    private int _nextId = 1;

    public ObservableCollection<TaskItem> Tasks { get; }
    public ObservableCollection<TaskItem> SelectedTasks { get; } = [];

    [ObservableProperty] private bool _canReorderRows = true;
    [ObservableProperty] private string _dropInfo = string.Empty;
    [ObservableProperty] private TaskItem? _selectedTask;

    public DragDropDemoViewModel()
    {
        Tasks = new ObservableCollection<TaskItem>(GenerateTasks(25));
    }

    [RelayCommand]
    private void AddTask()
    {
        Tasks.Add(CreateRandomTask());
    }

    [RelayCommand]
    private void ResetData()
    {
        Tasks.Clear();
        _nextId = 1;
        foreach (var task in GenerateTasks(25))
            Tasks.Add(task);
        DropInfo = string.Empty;
    }

    [RelayCommand]
    private void MoveToTop()
    {
        if (SelectedTasks.Count == 0) return;
        foreach (var task in SelectedTasks.ToList())
        {
            Tasks.Remove(task);
            Tasks.Insert(0, task);
        }
        DropInfo = $"已将 {SelectedTasks.Count} 项移至顶部";
    }

    [RelayCommand]
    private void MoveToBottom()
    {
        if (SelectedTasks.Count == 0) return;
        foreach (var task in SelectedTasks.ToList())
        {
            Tasks.Remove(task);
        }
        foreach (var task in SelectedTasks)
        {
            Tasks.Add(task);
        }
        DropInfo = $"已将 {SelectedTasks.Count} 项移至底部";
    }

    [RelayCommand]
    private void Save()
    {
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
            string.Empty
        );
    }
}
