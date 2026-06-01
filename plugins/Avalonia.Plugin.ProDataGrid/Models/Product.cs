using CommunityToolkit.Mvvm.ComponentModel;

namespace Avalonia.Plugin.ProDataGrid.Models;

public partial class Product : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private double _price;
    [ObservableProperty] private int _quantity;
    [ObservableProperty] private double _total;

    public Product() { }

    public Product(string name, double price, int quantity)
    {
        Name = name;
        Price = price;
        Quantity = quantity;
        Total = price * quantity;
    }

    partial void OnPriceChanged(double value)
    {
        Total = value * Quantity;
    }

    partial void OnQuantityChanged(int value)
    {
        Total = Price * value;
    }
}
