using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaFluentUI.Controls;
using LYBox.Layout.Fluent.Controls;

namespace LYBox.Layout.Fluent.Pages;

public partial class FlyoutPage : ViewBase
{
    public FlyoutPage() : base("Flyout")
    {
        InitializeComponent();
        
        CodeCards = new Dictionary<string, CodeCard>()
        {
            {"Flyout", FlyoutCard},
            {"TeachingTip", TeachingTipCard}
        };
    }
}
