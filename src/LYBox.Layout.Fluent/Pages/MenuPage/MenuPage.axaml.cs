using System.Collections.Generic;
using LYBox.Layout.Fluent.Controls;

namespace LYBox.Layout.Fluent.Pages;

public partial class MenuPage : ViewBase
{
    public MenuPage() : base("Menu")
    {
        InitializeComponent();
        
        CodeCards = new Dictionary<string, CodeCard>()
        {
            {"MenuBar", MenuBarCard},
        };
    }
}
