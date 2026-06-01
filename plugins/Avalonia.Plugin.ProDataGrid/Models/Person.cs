using CommunityToolkit.Mvvm.ComponentModel;

namespace Avalonia.Plugin.ProDataGrid.Models;

public partial class Person : ObservableObject
{
    [ObservableProperty] private int _id;
    [ObservableProperty] private string _firstName = string.Empty;
    [ObservableProperty] private string _lastName = string.Empty;
    [ObservableProperty] private int _age;
    [ObservableProperty] private string _city = string.Empty;
    [ObservableProperty] private string _department = string.Empty;
    [ObservableProperty] private double _salary;

    public Person() { }

    public Person(int id, string firstName, string lastName, int age, string city, string department, double salary)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        Age = age;
        City = city;
        Department = department;
        Salary = salary;
    }
}
