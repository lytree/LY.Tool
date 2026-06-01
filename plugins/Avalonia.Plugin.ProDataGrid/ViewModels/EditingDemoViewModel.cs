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
using System.Collections.Specialized;

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

    private readonly Stack<EditAction> _undoStack = new();
    private readonly Stack<EditAction> _redoStack = new();
    private const int MaxUndoSteps = 50;
    private const int MaxLogEntries = 200;

    public ObservableCollection<EditingDemoRow> Rows { get; }
    public ObservableCollection<EditingDemoRow> SelectedRows { get; } = [];
    public ObservableCollection<string> CategoryOptions { get; } = new(Categories);
    public ObservableCollection<string> SupplierOptions { get; } = new(Suppliers);

    private readonly RingBuffer<string> _logBuffer = new(MaxLogEntries);
    public ObservableCollection<string> EventLog { get; } = [];

    [ObservableProperty] private EditingDemoRow? _selectedRow;
    [ObservableProperty] private int _editingModeIndex = 0;
    [ObservableProperty] private IDataGridEditingInteractionModel? _editingInteractionModel;
    [ObservableProperty] private DataGridEditTriggers _editTriggers = DataGridEditTriggers.CellDoubleClick | DataGridEditTriggers.F2;
    [ObservableProperty] private bool _isReadOnly;
    [ObservableProperty] private string _modeDescription = string.Empty;
    [ObservableProperty] private string _editStatus = "就绪";
    [ObservableProperty] private int _dirtyCount;
    [ObservableProperty] private bool _canUndo;
    [ObservableProperty] private bool _canRedo;
    [ObservableProperty] private string _batchCategory = string.Empty;
    [ObservableProperty] private string _batchSupplier = string.Empty;

    public EditingDemoViewModel()
    {
        Rows = new ObservableCollection<EditingDemoRow>(GenerateRows(25));
        Rows.CollectionChanged += OnRowsCollectionChanged;
        foreach (var row in Rows)
            row.PropertyChanged += OnRowPropertyChanged;
        UpdateEditingMode(0);
    }

    private void OnRowsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (EditingDemoRow row in e.NewItems)
                row.PropertyChanged += OnRowPropertyChanged;
        }
        if (e.OldItems is not null)
        {
            foreach (EditingDemoRow row in e.OldItems)
                row.PropertyChanged -= OnRowPropertyChanged;
        }
        UpdateDirtyCount();
    }

    private void OnRowPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditingDemoRow.IsDirty))
            UpdateDirtyCount();
    }

    private void UpdateDirtyCount()
    {
        DirtyCount = Rows.Count(r => r.IsDirty);
    }

    partial void OnEditingModeIndexChanged(int value)
    {
        UpdateEditingMode(value);
    }

    [RelayCommand]
    private void AddRow()
    {
        var row = CreateRandomRow();
        Rows.Add(row);
        PushUndo(new EditAction(EditActionType.Add, row, Rows.Count - 1));
        EditStatus = $"已添加行: {row.Product}";
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        if (SelectedRow is not null)
        {
            var index = Rows.IndexOf(SelectedRow);
            if (index >= 0)
            {
                var snapshot = CloneRow(SelectedRow);
                PushUndo(new EditAction(EditActionType.Remove, snapshot, index));
                Rows.RemoveAt(index);
                EditStatus = $"已删除行: {snapshot.Product}";
            }
            SelectedRow = null;
        }
    }

    [RelayCommand]
    private void ResetData()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        UpdateUndoRedoState();

        foreach (var row in Rows)
            row.PropertyChanged -= OnRowPropertyChanged;
        Rows.Clear();
        _nextId = 1;
        var newRows = GenerateRows(25);
        foreach (var row in newRows)
            Rows.Add(row);
        EventLog.Clear();
        _logBuffer.Clear();
        EditStatus = "数据已重置";
        DirtyCount = 0;
    }

    [RelayCommand]
    private void ClearLog()
    {
        EventLog.Clear();
        _logBuffer.Clear();
    }

    [RelayCommand]
    private void Undo()
    {
        if (_undoStack.Count == 0) return;
        var action = _undoStack.Pop();
        _redoStack.Push(action);
        ApplyUndo(action);
        UpdateUndoRedoState();
        EditStatus = $"已撤销: {action.Type}";
    }

    [RelayCommand]
    private void Redo()
    {
        if (_redoStack.Count == 0) return;
        var action = _redoStack.Pop();
        _undoStack.Push(action);
        ApplyRedo(action);
        UpdateUndoRedoState();
        EditStatus = $"已重做: {action.Type}";
    }

    [RelayCommand]
    private void CommitDirty()
    {
        foreach (var row in Rows.Where(r => r.IsDirty))
            row.IsDirty = false;
        _undoStack.Clear();
        _redoStack.Clear();
        UpdateUndoRedoState();
        EditStatus = "所有修改已提交";
    }

    [RelayCommand]
    private void BatchSetCategory()
    {
        if (string.IsNullOrWhiteSpace(BatchCategory)) return;
        var targets = SelectedRows.Count > 0 ? SelectedRows.ToList() : Rows.ToList();
        var changes = new List<(int index, EditingDemoRow before)>();
        for (int i = 0; i < Rows.Count; i++)
        {
            if (targets.Contains(Rows[i]) && Rows[i].Category != BatchCategory)
            {
                changes.Add((i, CloneRow(Rows[i])));
                Rows[i].Category = BatchCategory;
            }
        }
        if (changes.Count > 0)
        {
            PushUndo(new EditAction(EditActionType.BatchEdit, changes));
            EditStatus = $"已批量设置 {changes.Count} 行分类为: {BatchCategory}";
        }
    }

    [RelayCommand]
    private void BatchSetSupplier()
    {
        if (string.IsNullOrWhiteSpace(BatchSupplier)) return;
        var targets = SelectedRows.Count > 0 ? SelectedRows.ToList() : Rows.ToList();
        var changes = new List<(int index, EditingDemoRow before)>();
        for (int i = 0; i < Rows.Count; i++)
        {
            if (targets.Contains(Rows[i]) && Rows[i].Supplier != BatchSupplier)
            {
                changes.Add((i, CloneRow(Rows[i])));
                Rows[i].Supplier = BatchSupplier;
            }
        }
        if (changes.Count > 0)
        {
            PushUndo(new EditAction(EditActionType.BatchEdit, changes));
            EditStatus = $"已批量设置 {changes.Count} 行供应商为: {BatchSupplier}";
        }
    }

    public void RecordCellEdit(EditingDemoRow row, string propertyName, object? oldValue, object? newValue)
    {
        var index = Rows.IndexOf(row);
        if (index < 0) return;
        var before = CloneRow(row);
        SetPropertyValue(before, propertyName, oldValue);
        PushUndo(new EditAction(EditActionType.CellEdit, (index, before, propertyName, oldValue, newValue)));
        AddLog($"编辑: 行{index} [{propertyName}] {oldValue} → {newValue}");
    }

    public void AddLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var entry = $"[{timestamp}] {message}";
        _logBuffer.Add(entry);
        EventLog.Insert(0, entry);
        while (EventLog.Count > MaxLogEntries)
            EventLog.RemoveAt(EventLog.Count - 1);
    }

    private void PushUndo(EditAction action)
    {
        _undoStack.Push(action);
        if (_undoStack.Count > MaxUndoSteps)
        {
            var stack = new Stack<EditAction>();
            for (int i = 0; i < MaxUndoSteps - 1; i++)
                stack.Push(_undoStack.Pop());
            _undoStack.Clear();
            while (stack.Count > 0)
                _undoStack.Push(stack.Pop());
        }
        _redoStack.Clear();
        UpdateUndoRedoState();
    }

    private void ApplyUndo(EditAction action)
    {
        switch (action.Type)
        {
            case EditActionType.CellEdit:
                var (idx, before, prop, _, _) = ((int, EditingDemoRow, string, object?, object?))action.Data!;
                if (idx >= 0 && idx < Rows.Count)
                {
                    var val = GetPropertyValue(before, prop);
                    SetPropertyValue(Rows[idx], prop, val);
                }
                break;
            case EditActionType.Add:
                var addedRow = (EditingDemoRow)action.Data!;
                var addIdx = action.Index;
                if (addIdx >= 0 && addIdx < Rows.Count && Rows[addIdx] == addedRow)
                    Rows.RemoveAt(addIdx);
                else
                    Rows.Remove(addedRow);
                break;
            case EditActionType.Remove:
                var removedRow = (EditingDemoRow)action.Data!;
                var removeIdx = Math.Min(action.Index, Rows.Count);
                Rows.Insert(removeIdx, removedRow);
                break;
            case EditActionType.BatchEdit:
                var changes = (List<(int index, EditingDemoRow before)>)action.Data!;
                foreach (var (changeIdx, changeBefore) in changes)
                {
                    if (changeIdx >= 0 && changeIdx < Rows.Count)
                        CopyRowData(changeBefore, Rows[changeIdx]);
                }
                break;
        }
    }

    private void ApplyRedo(EditAction action)
    {
        switch (action.Type)
        {
            case EditActionType.CellEdit:
                var (idx, _, prop, _, newVal) = ((int, EditingDemoRow, string, object?, object?))action.Data!;
                if (idx >= 0 && idx < Rows.Count)
                    SetPropertyValue(Rows[idx], prop, newVal);
                break;
            case EditActionType.Add:
                var addedRow = (EditingDemoRow)action.Data!;
                var addIdx = Math.Min(action.Index, Rows.Count);
                Rows.Insert(addIdx, addedRow);
                break;
            case EditActionType.Remove:
                var removeIdx = action.Index;
                if (removeIdx >= 0 && removeIdx < Rows.Count)
                    Rows.RemoveAt(removeIdx);
                break;
            case EditActionType.BatchEdit:
                break;
        }
    }

    private void UpdateUndoRedoState()
    {
        CanUndo = _undoStack.Count > 0;
        CanRedo = _redoStack.Count > 0;
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

    private static EditingDemoRow CloneRow(EditingDemoRow source)
    {
        return new EditingDemoRow(
            source.Id, source.Product, source.Category, source.Price,
            source.Quantity, source.InStock, source.LastUpdated,
            source.Supplier, source.Notes)
        { IsDirty = source.IsDirty };
    }

    private static void CopyRowData(EditingDemoRow source, EditingDemoRow target)
    {
        target.Product = source.Product;
        target.Category = source.Category;
        target.Price = source.Price;
        target.Quantity = source.Quantity;
        target.InStock = source.InStock;
        target.LastUpdated = source.LastUpdated;
        target.Supplier = source.Supplier;
        target.Notes = source.Notes;
        target.IsDirty = source.IsDirty;
    }

    private static object? GetPropertyValue(EditingDemoRow row, string propertyName)
    {
        return propertyName switch
        {
            nameof(EditingDemoRow.Product) => row.Product,
            nameof(EditingDemoRow.Category) => row.Category,
            nameof(EditingDemoRow.Price) => row.Price,
            nameof(EditingDemoRow.Quantity) => row.Quantity,
            nameof(EditingDemoRow.InStock) => row.InStock,
            nameof(EditingDemoRow.Supplier) => row.Supplier,
            nameof(EditingDemoRow.Notes) => row.Notes,
            _ => null
        };
    }

    private static void SetPropertyValue(EditingDemoRow row, string propertyName, object? value)
    {
        switch (propertyName)
        {
            case nameof(EditingDemoRow.Product): row.Product = (string)value!; break;
            case nameof(EditingDemoRow.Category): row.Category = (string)value!; break;
            case nameof(EditingDemoRow.Price): row.Price = (double)value!; break;
            case nameof(EditingDemoRow.Quantity): row.Quantity = (int)value!; break;
            case nameof(EditingDemoRow.InStock): row.InStock = (bool)value!; break;
            case nameof(EditingDemoRow.Supplier): row.Supplier = (string)value!; break;
            case nameof(EditingDemoRow.Notes): row.Notes = (string)value!; break;
        }
    }

    private enum EditActionType { CellEdit, Add, Remove, BatchEdit }

    private readonly record struct EditAction(EditActionType Type, object? Data, int Index = -1);
}

internal sealed class RingBuffer<T>
{
    private readonly T[] _items;
    private int _head;
    private int _count;

    public RingBuffer(int capacity)
    {
        _items = new T[capacity];
        _head = 0;
        _count = 0;
    }

    public int Count => _count;

    public void Add(T item)
    {
        _items[_head] = item;
        _head = (_head + 1) % _items.Length;
        if (_count < _items.Length)
            _count++;
    }

    public void Clear()
    {
        _head = 0;
        _count = 0;
        Array.Clear(_items);
    }
}
