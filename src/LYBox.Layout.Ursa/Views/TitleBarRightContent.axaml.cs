using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Services;

namespace LYBox.Layout.Ursa.Views;

public partial class TitleBarRightContent : UserControl
{
    public TitleBarRightContent()
    {
        InitializeComponent();
    }


    private void LangSwitchButton_Clicked(object? sender, RoutedEventArgs e)
    {
        var localizationService = ServiceLocator.GetService<ILocalizationService>();
        if (localizationService is null) return;
        var current = localizationService.CurrentCulture;
        var condition = string.Equals(current.TwoLetterISOLanguageName, "zh");
        localizationService.SetCulture(condition ? CultureInfo.InvariantCulture : new CultureInfo("zh-Hans"));
    }
}
