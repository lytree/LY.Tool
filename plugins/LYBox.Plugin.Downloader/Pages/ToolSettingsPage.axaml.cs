using System.Collections.Specialized;
using Avalonia.Controls;
using LYBox.Plugin.Downloader.ViewModels;

namespace LYBox.Plugin.Downloader.Pages;

public partial class ToolSettingsPage : UserControl
{
    private ToolSettingsViewModel? _currentVm;

    public ToolSettingsPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_currentVm is not null)
        {
            _currentVm.LogEntries.CollectionChanged -= OnLogEntriesCollectionChanged;
        }

        _currentVm = DataContext as ToolSettingsViewModel;

        if (_currentVm is not null)
        {
            _currentVm.LogEntries.CollectionChanged += OnLogEntriesCollectionChanged;
        }
    }

    private void OnLogEntriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems?.Count > 0)
        {
            LogListBox.ScrollIntoView(e.NewItems.Count == 1
                ? e.NewItems[0]!
                : e.NewItems[e.NewItems.Count - 1]!);
        }
    }
}
