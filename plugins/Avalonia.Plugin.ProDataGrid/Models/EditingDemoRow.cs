using CommunityToolkit.Mvvm.ComponentModel;

namespace Avalonia.Plugin.ProDataGrid.Models;

public partial class EditingDemoRow : ObservableObject
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
    }

    public double Total => Price * Quantity;
}
