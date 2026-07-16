using System.Collections.Generic;
using LYBox.Layout.Fluent.Controls;

namespace LYBox.Layout.Fluent.Pages;

public partial class BreadcrumbBarPage : ViewBase
{
    public BreadcrumbBarPage() : base("BreadcrumbBar")
    {
        InitializeComponent();
        
        CodeCards = new Dictionary<string, CodeCard>()
        {
            {"BreadcrumbBar", BreadcrumbBarCard},
        };
    }
}
