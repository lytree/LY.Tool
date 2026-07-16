using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AvaloniaFluentUI.Controls;
using LYBox.Layout.Fluent.Controls;

namespace LYBox.Layout.Fluent.Views;

public partial class MenuAndToolBarView : UserControl 
{
    public MenuAndToolBarView()// : base("MenuAndToolBar")
    {
        InitializeComponent();
        
        // CodeCards = new Dictionary<string, CodeCard>()
        // {
            // {"Menu", MenuCard},
            // {"MenuBar", MenuBarCard},
            // {"CommandBar", CommandBarCard},
            // {"CommandBarFlyout", CommandBarFlyoutCard}
        // };
    }
}
