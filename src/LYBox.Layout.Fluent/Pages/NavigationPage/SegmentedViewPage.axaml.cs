using System;
using System.Collections.Generic;
using Avalonia.Controls;
using AvaloniaFluentUI.Controls;
using LYBox.Layout.Fluent.Controls;

namespace LYBox.Layout.Fluent.Pages;

public partial class SegmentedViewPage : ViewBase
{
    public SegmentedViewPage()  : base("SegmentedView")
    {
        InitializeComponent();
        
        CodeCards = new Dictionary<string, CodeCard>()
        {
            {"Segmented", SegmentedCard}
        };
    }

    private void OnSelectedItemChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is SegmentedView sv)
        {
            Console.WriteLine($"Selected Item Changed -> Index: {sv.SelectedIndex}, Value: {sv.SelectedItem}");
        }
    }
}
