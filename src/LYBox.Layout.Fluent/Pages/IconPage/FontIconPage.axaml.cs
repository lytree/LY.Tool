using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaFluentUI.Controls;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LYBox.Layout.Fluent.Controls;
using LYBox.Layout.Fluent.Messages.IconViewMessages;

namespace LYBox.Layout.Fluent.Pages.IconPage;

public partial class FontIconPage : UserControl
{
    public FontIconPage()
    {
        InitializeComponent();
    }
}

