using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using AvaloniaFluentUI.Controls;
using AvaloniaFluentUI.Locale;
using AvaloniaFluentUI.Media.Animation;
using LYBox.Layout.Fluent.Controls;
using LYBox.Layout.Fluent.Models;
using LYBox.Layout.Fluent.Pages;
using LYBox.Layout.Fluent.ViewModels;

namespace LYBox.Layout.Fluent.Views;

public partial class NavigationView : UserControl 
{
    public NavigationView()// : base("Navigation")
    {
        InitializeComponent();
        
        // CodeCards = new Dictionary<string, CodeCard>()
        // {
            // {"NavigationView", NavigationViewCard},
            // {"PageTransition", PageTransitionCard},
            // {"TabView", TabViewCard},
            // {"BreadcrumbBar", BreadcrumbBarCard},
            // {"Segmented", SegmentedCard}
        // };
    }
}
