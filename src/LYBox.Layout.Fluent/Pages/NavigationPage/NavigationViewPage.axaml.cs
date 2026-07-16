using System.Collections.Generic;
using LYBox.Layout.Fluent.Controls;

namespace LYBox.Layout.Fluent.Pages;

public partial class NavigationViewPage : ViewBase
{
    public NavigationViewPage() : base("NavigationView")
    {
        InitializeComponent();
        
        CodeCards = new Dictionary<string, CodeCard>()
        {
            {"NavigationView", NavigationViewCard},
        };
    }
}
