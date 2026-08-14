
namespace LYBox.Plugin.Shared.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class NavigationItemAttribute(string key) : Attribute
{
    public string Key { get; } = key;
}