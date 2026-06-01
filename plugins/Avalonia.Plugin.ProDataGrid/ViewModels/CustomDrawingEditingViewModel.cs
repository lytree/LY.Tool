using Avalonia.Plugin.ProDataGrid.Models;
using Avalonia.Plugin.ProDataGrid.Pages;
using Avalonia.Plugin.Shared.Attributes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace Avalonia.Plugin.ProDataGrid.ViewModels;

[NavigationItem("KeyCustomDrawingEditing")]
[Menu("NAV_CustomDrawingEditing", "KeyCustomDrawingEditing", "NAV_ProDataGrid")]
[ViewMap(typeof(CustomDrawingEditingPage))]
public partial class CustomDrawingEditingViewModel : ObservableObject
{
    public CustomDrawingEditingViewModel()
    {
        Rows = [];
        ResetRows();
    }

    public ObservableCollection<CustomDrawingEditingRow> Rows { get; }

    [RelayCommand]
    private void AddRow()
    {
        int nextId = Rows.Count + 1;
        Rows.Add(new CustomDrawingEditingRow
        {
            Id = nextId,
            Title = $"任务 {nextId}",
            Notes = "通过命令新建的可编辑行。",
            Category = "草稿"
        });
    }

    [RelayCommand]
    private void ResetRows()
    {
        Rows.Clear();
        Rows.Add(new CustomDrawingEditingRow
        {
            Id = 1,
            Title = "发布验证",
            Notes = "验证自定义绘制文本编辑和提交行为。",
            Category = "测试"
        });
        Rows.Add(new CustomDrawingEditingRow
        {
            Id = 2,
            Title = "性能记录",
            Notes = "跟踪编辑频繁更新单元格时的滚动流畅度。",
            Category = "性能"
        });
        Rows.Add(new CustomDrawingEditingRow
        {
            Id = 3,
            Title = "文档更新",
            Notes = "为可编辑自定义绘制列编写使用指南。",
            Category = "文档"
        });
        Rows.Add(new CustomDrawingEditingRow
        {
            Id = 4,
            Title = "回归排查",
            Notes = "切换选项卡并重新选择单元格，验证前景色更新一致性。",
            Category = "稳定性"
        });
    }
}
