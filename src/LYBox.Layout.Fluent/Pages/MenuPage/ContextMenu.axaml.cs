using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using LYBox.Layout.Fluent.Controls;
using LYBox.Layout.Fluent.Extensions;

namespace LYBox.Layout.Fluent.Pages;

public partial class ContextMenuPage : ViewBase
{
    public ContextMenuPage() : base("ContextMenu")
    {
        InitializeComponent();
        
        CodeCards = new Dictionary<string, CodeCard>()
        {
            {"Menu", MenuCard},
        };
    }
}
