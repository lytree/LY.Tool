using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.ProDataGrid.Models;
using LYBox.Plugin.ProDataGrid.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace LYBox.Plugin.ProDataGrid.ViewModels;

[NavigationItem("KeyBasicDataGrid")]
[Menu("NAV_BasicDataGrid", "KeyBasicDataGrid", "NAV_ProDataGrid", IconName = "Table")]
[ViewMap(typeof(BasicDataGridDemo))]
public partial class BasicDataGridDemoViewModel : ObservableObject
{
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
        ExportHelper.ExportToJson(People, "people_export.json");
    }

    private void ApplyFilter()
    {
        var filtered = string.IsNullOrWhiteSpace(FilterText)
            ? _allPeople
            : _allPeople.Where(MatchesFilter).ToList();

        People.SyncWith(filtered);
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
        var list = new List<Person>(count);
        for (int i = 0; i < count; i++)
            list.Add(CreateRandomPerson());
        return list;
    }

    private Person CreateRandomPerson()
    {
        return new Person(
            _nextId++,
            TestData.FirstNames[_random.Next(TestData.FirstNames.Length)],
            TestData.LastNames[_random.Next(TestData.LastNames.Length)],
            _random.Next(22, 60),
            TestData.Cities[_random.Next(TestData.Cities.Length)],
            TestData.Departments[_random.Next(TestData.Departments.Length)],
            Math.Round(_random.NextDouble() * 45000 + 8000, 2)
        );
    }
}
