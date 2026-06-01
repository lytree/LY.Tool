using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.ProDataGrid.Models;
using Avalonia.Plugin.ProDataGrid.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace Avalonia.Plugin.ProDataGrid.ViewModels;

[NavigationItem("KeyFormulaDataGrid")]
[Menu("NAV_FormulaDataGrid", "KeyFormulaDataGrid", "NAV_ProDataGrid")]
[ViewMap(typeof(FormulaDataGridDemo))]
public partial class FormulaDataGridDemoViewModel : ObservableObject
{
    private static readonly (string Name, double MinPrice, double MaxPrice)[] ProductCatalog =
    [
        ("ThinkPad X1 Carbon 笔记本电脑", 8999, 14999),
        ("罗技 MX Master 3S 无线鼠标", 599, 799),
        ("HHKB Professional 键盘", 1999, 2499),
        ("戴尔 U2723QE 4K显示器", 3299, 4599),
        ("索尼 WH-1000XM5 降噪耳机", 2299, 2999),
        ("Anker 100W USB-C 充电线", 89, 149),
        ("罗技 C920 高清摄像头", 499, 699),
        ("哈曼卡顿 SoundSticks 音响", 899, 1299),
        ("iPad Pro 12.9 平板电脑", 8999, 12999),
        ("Anker 65W 氮化镓充电器", 199, 349),
        ("微软 Surface Arc 鼠标", 499, 699),
        ("雷蛇 BlackWidow 机械键盘", 699, 1199),
        ("三星 T7 移动固态硬盘 1TB", 599, 899),
        ("绿联 12合1 USB-C 扩展坞", 299, 499),
        ("明基 ScreenBar 屏幕挂灯", 699, 899),
        ("Wacom Intuos 数位板", 499, 999),
        ("罗技 C922 Pro 摄像头", 699, 899),
        ("小米 34寸曲面显示器", 2499, 3299),
        ("苹果 Magic Keyboard 妙控键盘", 699, 999),
        ("华为 MateView GT 曲面屏", 3999, 5499)
    ];
    private static readonly Random _random = new();

    public ObservableCollection<Product> Products { get; }

    [ObservableProperty] private Product? _selectedProduct;

    public double TotalPrice => Products.Sum(p => p.Price);
    public int TotalQuantity => Products.Sum(p => p.Quantity);
    public double GrandTotal => Products.Sum(p => p.Total);

    public FormulaDataGridDemoViewModel()
    {
        Products = new ObservableCollection<Product>(GenerateProducts(15));
        Products.CollectionChanged += (_, _) => UpdateSummary();
    }

    [RelayCommand]
    private void AddProduct()
    {
        Products.Add(CreateRandomProduct());
    }

    [RelayCommand]
    private void ResetData()
    {
        Products.Clear();
        foreach (var product in GenerateProducts(15))
            Products.Add(product);
    }

    [RelayCommand]
    private void Save()
    {
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        OnPropertyChanged(nameof(TotalPrice));
        OnPropertyChanged(nameof(TotalQuantity));
        OnPropertyChanged(nameof(GrandTotal));
    }

    private List<Product> GenerateProducts(int count)
    {
        var list = new List<Product>();
        for (int i = 0; i < count; i++)
            list.Add(CreateRandomProduct());
        return list;
    }

    private Product CreateRandomProduct()
    {
        var catalog = ProductCatalog[_random.Next(ProductCatalog.Length)];
        var price = Math.Round(_random.NextDouble() * (catalog.MaxPrice - catalog.MinPrice) + catalog.MinPrice, 2);
        var quantity = _random.Next(1, 50);
        return new Product(catalog.Name, price, quantity);
    }
}
