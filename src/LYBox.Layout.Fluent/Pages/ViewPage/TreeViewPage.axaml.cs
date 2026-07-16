using System.Collections.Generic;
using LYBox.Layout.Fluent.Controls;

namespace LYBox.Layout.Fluent.Pages;

public partial class TreeViewPage : ViewBase
{
    public TreeViewPage()  : base("TreeView")
    {
        InitializeComponent();
        
        CodeCards = new Dictionary<string, CodeCard>()
        {
            {"TreeView", TreeViewCard},
        };
    }
}
