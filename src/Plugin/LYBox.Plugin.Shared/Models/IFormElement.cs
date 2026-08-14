using System.Collections.ObjectModel;

namespace LYBox.Plugin.Shared.Models;

/// <summary>
/// 表单元素接口
/// </summary>
public interface IFormElement
{
}

/// <summary>
/// 表单组视图模型接口
/// </summary>
public interface IFormGroupViewModel : IFormElement
{
    string? Title { get; set; }
    ObservableCollection<IFromItemViewModel> Items { get; set; }
}

/// <summary>
/// 表单项视图模型接口
/// </summary>
public interface IFromItemViewModel : IFormElement
{
    string? Label { get; set; }
}
