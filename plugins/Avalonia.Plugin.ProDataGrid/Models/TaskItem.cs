using CommunityToolkit.Mvvm.ComponentModel;

namespace Avalonia.Plugin.ProDataGrid.Models;

public partial class TaskItem : ObservableObject
{
    [ObservableProperty] private int _id;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _assignee = string.Empty;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private string _priority = string.Empty;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private DateTime _dueDate;
    [ObservableProperty] private string _category = string.Empty;
    [ObservableProperty] private string _description = string.Empty;

    public TaskItem() { }

    public TaskItem(int id, string title, string assignee, string status,
        string priority, double progress, DateTime dueDate, string category, string description)
    {
        Id = id;
        Title = title;
        Assignee = assignee;
        Status = status;
        Priority = priority;
        Progress = progress;
        DueDate = dueDate;
        Category = category;
        Description = description;
    }
}
