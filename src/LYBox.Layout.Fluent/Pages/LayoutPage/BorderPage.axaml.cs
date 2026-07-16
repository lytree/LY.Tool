using System.Collections.Generic;
using LYBox.Layout.Fluent.Controls;

namespace LYBox.Layout.Fluent.Pages;

public partial class BorderPage : ViewBase
{
    public BorderPage() : base("Border")
    {
        InitializeComponent();
        
        CodeCards = new Dictionary<string, CodeCard>()
        {
            {"Border", BorderCard},
            {"Canvas", CanvasCard},
        };
    }
}
