using System.Collections.Generic;
using LYBox.Layout.Fluent.Controls;

namespace LYBox.Layout.Fluent.Pages;

public partial class ListPage : ViewBase
{
    public ListPage() : base("List")
    {
        InitializeComponent();
        
        CodeCards = new Dictionary<string, CodeCard>()
        {
            {"ListBox", ListBoxCard},
        };
    }
}
