using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AvaloniaFluentUI.Controls;
using LYBox.Layout.Fluent.Controls;

namespace LYBox.Layout.Fluent.Pages;

public partial class ComboBoxPage : ViewBase 
{
    public ComboBoxPage() : base("ComboBox")
    {
        InitializeComponent();

        CodeCards = new Dictionary<string, CodeCard>()
        {
            {"ComboBox", ComboBoxCard},
        };
    }
}

