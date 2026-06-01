using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Avalonia.Plugin.ProDataGrid.Models;

public partial class EditingDemoRow : ObservableObject, IDataErrorInfo
{
    [ObservableProperty] private int _id;
    [ObservableProperty] private string _product = string.Empty;
    [ObservableProperty] private string _category = string.Empty;
    [ObservableProperty] private double _price;
    [ObservableProperty] private int _quantity;
    [ObservableProperty] private bool _inStock = true;
    [ObservableProperty] private DateTime _lastUpdated = DateTime.Now;
    [ObservableProperty] private string _supplier = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private bool _isDirty;

    public EditingDemoRow() { }

    public EditingDemoRow(int id, string product, string category, double price,
        int quantity, bool inStock, DateTime lastUpdated, string supplier, string notes)
    {
        Id = id;
        Product = product;
        Category = category;
        Price = price;
        Quantity = quantity;
        InStock = inStock;
        LastUpdated = lastUpdated;
        Supplier = supplier;
        Notes = notes;
        IsDirty = false;
    }

    public double Total => Price * Quantity;

    public string Error => string.Empty;

    public string this[string columnName] => columnName switch
    {
        nameof(Product) => string.IsNullOrWhiteSpace(Product) ? "产品名不能为空" : string.Empty,
        nameof(Price) => Price < 0 ? "价格不能为负数" : string.Empty,
        nameof(Quantity) => Quantity < 0 ? "数量不能为负数" : string.Empty,
        nameof(Category) => string.IsNullOrWhiteSpace(Category) ? "分类不能为空" : string.Empty,
        nameof(Supplier) => string.IsNullOrWhiteSpace(Supplier) ? "供应商不能为空" : string.Empty,
        _ => string.Empty
    };

    partial void OnProductChanged(string value) => MarkDirty();
    partial void OnCategoryChanged(string value) => MarkDirty();
    partial void OnPriceChanged(double value)
    {
        OnPropertyChanged(nameof(Total));
        MarkDirty();
    }
    partial void OnQuantityChanged(int value)
    {
        OnPropertyChanged(nameof(Total));
        MarkDirty();
    }
    partial void OnInStockChanged(bool value) => MarkDirty();
    partial void OnSupplierChanged(string value) => MarkDirty();
    partial void OnNotesChanged(string value) => MarkDirty();

    private void MarkDirty()
    {
        IsDirty = true;
        LastUpdated = DateTime.Now;
    }
}
