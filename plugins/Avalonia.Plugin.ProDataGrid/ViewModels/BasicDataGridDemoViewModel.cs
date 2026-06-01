using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.ProDataGrid.Models;
using Avalonia.Plugin.ProDataGrid.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace Avalonia.Plugin.ProDataGrid.ViewModels;

[NavigationItem("KeyBasicDataGrid")]
[Menu("NAV_BasicDataGrid", "KeyBasicDataGrid", "NAV_ProDataGrid")]
[ViewMap(typeof(BasicDataGridDemo))]
public partial class BasicDataGridDemoViewModel : ObservableObject
{
    private static readonly string[] FirstNames =
    [
        "张伟", "王芳", "李娜", "刘洋", "陈静", "杨磊", "赵敏", "黄强",
        "周婷", "吴鹏", "徐明", "孙丽", "马超", "朱军", "胡雪", "郭涛",
        "林峰", "何欣", "罗杰", "梁宇", "宋佳", "唐浩", "韩冰", "冯颖"
    ];
    private static readonly string[] LastNames =
    [
        "王", "李", "张", "刘", "陈", "杨", "赵", "黄",
        "周", "吴", "徐", "孙", "马", "朱", "胡", "郭"
    ];
    private static readonly string[] Cities =
    [
        "上海", "北京", "深圳", "杭州", "成都", "武汉", "南京", "苏州",
        "广州", "重庆", "西安", "长沙", "天津", "青岛", "大连", "厦门"
    ];
    private static readonly string[] Departments =
    [
        "研发部", "市场部", "销售部", "人力资源部", "财务部", "运营部", "法务部", "客服部",
        "产品部", "设计部", "质量部", "行政部"
    ];
    private static readonly Random _random = new();
    private int _nextId = 1;
    private List<Person> _allPeople;

    public ObservableCollection<Person> People { get; }

    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private Person? _selectedPerson;

    public BasicDataGridDemoViewModel()
    {
        _allPeople = GeneratePeople(30);
        People = new ObservableCollection<Person>(_allPeople);
    }

    partial void OnFilterTextChanged(string value)
    {
        ApplyFilter();
    }

    [RelayCommand]
    private void AddRow()
    {
        var person = CreateRandomPerson();
        _allPeople.Add(person);
        if (string.IsNullOrWhiteSpace(FilterText) || MatchesFilter(person))
            People.Add(person);
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        if (SelectedPerson is not null)
        {
            _allPeople.Remove(SelectedPerson);
            People.Remove(SelectedPerson);
            SelectedPerson = null;
        }
    }

    [RelayCommand]
    private void ResetData()
    {
        _nextId = 1;
        _allPeople = GeneratePeople(30);
        FilterText = string.Empty;
        People.Clear();
        foreach (var p in _allPeople)
            People.Add(p);
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
        People.Clear();
        var filtered = string.IsNullOrWhiteSpace(FilterText)
            ? _allPeople
            : _allPeople.Where(MatchesFilter);
        foreach (var p in filtered)
            People.Add(p);
    }

    private bool MatchesFilter(Person p)
    {
        var filter = FilterText.Trim();
        return p.FirstName.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || p.LastName.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || p.City.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || p.Department.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private List<Person> GeneratePeople(int count)
    {
        var list = new List<Person>();
        for (int i = 0; i < count; i++)
            list.Add(CreateRandomPerson());
        return list;
    }

    private Person CreateRandomPerson()
    {
        return new Person(
            _nextId++,
            FirstNames[_random.Next(FirstNames.Length)],
            LastNames[_random.Next(LastNames.Length)],
            _random.Next(22, 60),
            Cities[_random.Next(Cities.Length)],
            Departments[_random.Next(Departments.Length)],
            Math.Round(_random.NextDouble() * 45000 + 8000, 2)
        );
    }
}
