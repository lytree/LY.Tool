using Avalonia.Media;
using AvaloniaFluentUI.Locale;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using LYBox.Layout.Fluent.Messages.IconViewMessages;

namespace LYBox.Layout.Fluent.ViewModels;

public partial class FluentIconPageViewModel : ViewModelBase
{
    public override string Title => LocalizationService.Instance.GetString("Icon");

    public FluentIconPageViewModel()
    {
        WeakReferenceMessenger.Default.Register<CheckedIconChangedMessage>(this, OnCheckedIconChanged);
    }
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentItemEnumName))]
    private string _currentIconName = "";

    [ObservableProperty]
    private Geometry? _currentIconData;
    
    public string CurrentItemEnumName => CurrentIconName == "" ? "" : $"FluentIcon.{CurrentIconName}";

    private void OnCheckedIconChanged(object? sender, CheckedIconChangedMessage message)
    {
        CurrentIconName = message.Name;
        CurrentIconData = message.Data;
    }
}
