using Avalonia.Controls;
using Avalonia.Controls.DataGridEditing;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.ProDataGrid.Models;
using Avalonia.Plugin.ProDataGrid.Pages;
using Avalonia.Plugin.ProDataGrid.EditingModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace Avalonia.Plugin.ProDataGrid.ViewModels;

[NavigationItem("KeyEditingDemo")]
[Menu("NAV_EditingDemo", "KeyEditingDemo", "NAV_ProDataGrid")]
[ViewMap(typeof(EditingDemoPage))]
public partial class EditingDemoViewModel : ObservableObject
{
    private static readonly string[] Products =
    [
        "笔记本电脑", "无线鼠标", "机械键盘", "显示器", "USB集线器",
        "耳机", "摄像头", "移动硬盘", "路由器", "打印机",
        "扫描仪", "投影仪", "平板电脑", "手机", "智能手表"
    ];
    private static readonly string[] Categories =
    [
        "电脑", "外设", "存储", "网络", "办公设备", "移动设备"
    ];
    private static readonly string[] Suppliers =
    [
        "联想", "戴尔", "惠普", "罗技", "微软", "苹果",
        "华硕", "三星", "华为", "小米"
    ];
    private static readonly string[] Notes =
    [
        "热销商品", "新品上架", "促销中", "库存紧张", "即将到货",
        "需要预购", "限量版", "已停产", "替换型号已发布", ""
    ];
    private static readonly Random _random = new();
    private int _nextId = 1;

    public ObservableCollection<EditingDemoRow> Rows { get; }
    public ObservableCollection<string> CategoryOptions { get; } = new(Categories);
    public ObservableCollection<string> SupplierOptions { get; } = new(Suppliers);
    public ObservableCollection<string> EventLog { get; } = [];

    [ObservableProperty] private EditingDemoRow? _selectedRow;
    [ObservableProperty] private int _editingModeIndex = 0;
    [ObservableProperty] private IDataGridEditingInteractionModel? _editingInteractionModel;
    [ObservableProperty] private DataGridEditTriggers _editTriggers = DataGridEditTriggers.CellDoubleClick | DataGridEditTriggers.F2;
    [ObservableProperty] private bool _isReadOnly;
    [ObservableProperty] private string _modeDescription = string.Empty;
    [ObservableProperty] private string _editStatus = "就绪";

    public EditingDemoViewModel()
    {
        Rows = new ObservableCollection<EditingDemoRow>(GenerateRows(25));
        UpdateEditingMode(0);
    }

    partial void OnEditingModeIndexChanged(int value)
    {
        UpdateEditingMode(value);
    }

    [RelayCommand]
    private void AddRow()
    {
        Rows.Add(CreateRandomRow());
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        if (SelectedRow is not null)
        {
            Rows.Remove(SelectedRow);
            SelectedRow = null;
        }
    }

    [RelayCommand]
    private void ResetData()
    {
        Rows.Clear();
        _nextId = 1;
        foreach (var row in GenerateRows(25))
            Rows.Add(row);
        EventLog.Clear();
        EditStatus = "数据已重置";
    }

    [RelayCommand]
    private void ClearLog()
    {
        EventLog.Clear();
    }

    public void AddLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        EventLog.Insert(0, $"[{timestamp}] {message}");
        if (EventLog.Count > 200)
            EventLog.RemoveAt(EventLog.Count - 1);
    }

    private void UpdateEditingMode(int index)
    {
        switch (index)
        {
            case 0:
                IsReadOnly = false;
                EditingInteractionModel = new DoubleClickOnlyEditingInteractionModel();
                EditTriggers = DataGridEditTriggers.CellDoubleClick | DataGridEditTriggers.F2;
                ModeDescription = "双击或按 F2 进入编辑模式";
                EditStatus = "已切换到: 双击编辑";
                break;
            case 1:
                IsReadOnly = false;
                EditingInteractionModel = new DataGridEditingInteractionModel();
                EditTriggers = DataGridEditTriggers.CellClick | DataGridEditTriggers.CellDoubleClick |
                               DataGridEditTriggers.F2 | DataGridEditTriggers.TextInput;
                ModeDescription = "单击即可编辑单元格，支持双击、F2、直接输入";
                EditStatus = "已切换到: 单击编辑";
                break;
            case 2:
                IsReadOnly = false;
                EditingInteractionModel = new AltClickEditingInteractionModel();
                EditTriggers = DataGridEditTriggers.CellClick | DataGridEditTriggers.CellDoubleClick;
                ModeDescription = "按住 Alt + 单击进入编辑模式";
                EditStatus = "已切换到: Alt+点击编辑";
                break;
            case 3:
                IsReadOnly = true;
                EditingInteractionModel = null;
                EditTriggers = DataGridEditTriggers.None;
                ModeDescription = "只读模式，不可编辑";
                EditStatus = "已切换到: 只读";
                break;
        }
    }

    private List<EditingDemoRow> GenerateRows(int count)
    {
        var list = new List<EditingDemoRow>();
        for (int i = 0; i < count; i++)
            list.Add(CreateRandomRow());
        return list;
    }

    private EditingDemoRow CreateRandomRow()
    {
        var product = Products[_random.Next(Products.Length)];
        var category = Categories[_random.Next(Categories.Length)];
        var supplier = Suppliers[_random.Next(Suppliers.Length)];
        var price = Math.Round(_random.NextDouble() * 9900 + 100, 2);
        var quantity = _random.Next(1, 500);
        var inStock = _random.NextDouble() > 0.2;
        var note = Notes[_random.Next(Notes.Length)];

        return new EditingDemoRow(
            _nextId++,
            product,
            category,
            price,
            quantity,
            inStock,
            DateTime.Now.AddDays(-_random.Next(0, 365)),
            supplier,
            note
        );
    }
}
