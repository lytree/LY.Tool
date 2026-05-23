using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Plugin.TDLSharp.ViewModels;

namespace Avalonia.Plugin.TDLSharp.Pages;

public partial class TdlScriptPage : UserControl
{
    private TdlViewModelBase? _currentVm;

    public TdlScriptPage()
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

        _currentVm = DataContext as TdlViewModelBase;

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
