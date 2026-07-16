using System.Collections.Generic;
using LYBox.Layout.Fluent.Controls;

namespace LYBox.Layout.Fluent.Pages;

public partial class TextBlockPage : ViewBase
{
    public TextBlockPage() : base("TextBlock")
    {
        InitializeComponent();
        
        CodeCards = new Dictionary<string, CodeCard>()
        {
            {"TextBlock", TextBlockCard},
        };
    }
}
