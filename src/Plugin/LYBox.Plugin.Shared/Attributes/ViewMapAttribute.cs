
namespace LYBox.Plugin.Shared.Attributes;

// 1. 定义映射特性
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class ViewMapAttribute(Type viewType) : Attribute
{
    public Type ViewType { get; } = viewType;
}