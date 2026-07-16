using System.Collections.Generic;
using Avalonia.Controls;
using AvaloniaFluentUI.Locale;
using CommunityToolkit.Mvvm.Input;
using LYBox.Layout.Fluent.Models;
using LYBox.Layout.Fluent.Services;

namespace LYBox.Layout.Fluent.ViewModels;

public partial class TextViewModel : ViewModelBase
{
    public List<ButtonItemModel> TextItemSource { get; }
    public override string Title => LocalizationService.Instance.GetString("Text");
    
    public TextViewModel()
    {
        TextItemSource = ButtonItemModel.CreateList(
            ("TextBlock", "TextBlock", "TextBlock", "Text block, used to display text"),
            ("TextBox", "TextBox", "TextBox", "Text input box"),
            ("PasswordBox", "PasswordBox", "TextBox", "Password input box, which can be turned on and off to display the password"),
            ("NumberBox", "NumberBox", "NumberBox", "Numeric input box that can be fine-tuned")
        );
    }

}
