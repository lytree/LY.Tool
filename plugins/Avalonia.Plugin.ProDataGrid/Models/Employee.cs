using CommunityToolkit.Mvvm.ComponentModel;

namespace Avalonia.Plugin.ProDataGrid.Models;

public partial class Employee : ObservableObject
{
    [ObservableProperty] private int _id;
    [ObservableProperty] private string _firstName = string.Empty;
    [ObservableProperty] private string _lastName = string.Empty;
    [ObservableProperty] private string _department = string.Empty;
    [ObservableProperty] private string _position = string.Empty;
    [ObservableProperty] private double _salary;
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private DateTime _hireDate;
    [ObservableProperty] private int _performanceRating;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _city = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;

    public Employee() { }

    public Employee(int id, string firstName, string lastName, string department,
        string position, double salary, bool isActive, DateTime hireDate,
        int performanceRating, string email, string city, string notes)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        Department = department;
        Position = position;
        Salary = salary;
        IsActive = isActive;
        HireDate = hireDate;
        PerformanceRating = performanceRating;
        Email = email;
        City = city;
        Notes = notes;
    }

    public string FullName => $"{FirstName} {LastName}";
}
