using System.Collections.Generic;
using LYBox.Layout.Fluent.Controls;

namespace LYBox.Layout.Fluent.Pages;

public partial class SliderPage : ViewBase 
{
    public SliderPage() : base("Slider")
    {
        InitializeComponent();

        CodeCards = new Dictionary<string, CodeCard>() { { "Slider", SliderCard }, };
    }
}

