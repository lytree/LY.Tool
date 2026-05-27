using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Avalonia.Plugin.Shared;

public class ViewLocator : IDataTemplate
{
    private static readonly Dictionary<Type, ViewFactory> _viewRegistry = new(100);
    // ConditionalWeakTable 使用 DependentHandle，即使 Value(Key) 形成循环引用，
    // GC 仍能识别并回收孤立的对象对，解决 ViewModel→View→DataContext→ViewModel 循环引用导致的内存泄漏。
    private static ConditionalWeakTable<object, Control> _viewCache = new();

    public static void Register<TViewModel, TView>()
        where TView : Control, new()
    {
        _viewRegistry[typeof(TViewModel)] = () => new TView();
    }

    public static void RegisterRange(IEnumerable<KeyValuePair<Type, ViewFactory>> definitions)
    {
        foreach (var def in definitions)
        {
            _viewRegistry[def.Key] = def.Value;
        }
    }

    public static void RegisterPlugin(IPlugin plugin)
    {
        var definitions = plugin.GetViewDefinitions();
        if (definitions == null) return;

        foreach (var def in definitions)
        {
            _viewRegistry[def.Key] = def.Value;
        }
    }

    public static void InvalidateViewCache(object viewModel)
    {
        _viewCache.Remove(viewModel);
    }

    public static void InvalidateAllViewCache()
    {
        // ConditionalWeakTable 不支持 Clear，替换为新实例使旧表所有条目变为不可达，由 GC 自动回收。
        _viewCache = new ConditionalWeakTable<object, Control>();
    }

    public Control? Build(object? data)
    {
        if (data is null) return null;

        if (_viewCache.TryGetValue(data, out var cachedControl))
        {
            return cachedControl;
        }

        var type = data.GetType();

        if (_viewRegistry.TryGetValue(type, out var factory))
        {
            var control = factory();
            // 不显式设置 DataContext，由 Avalonia ContentControl 自动传播 DataContext。
            // 避免在 Build 中创建 View→ViewModel 强引用，让 ConditionalWeakTable 的
            // DependentHandle 机制能正确处理循环引用的 GC 回收。
            _viewCache.Add(data, control);
            return control;
        }

        return new TextBlock
        {
            Text = $"View not found for: {type.FullName}. \nPlease ensure it is registered in IPlugin.GetViewDefinitions().",
            VerticalAlignment = Layout.VerticalAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };
    }

    public bool Match(object? data) => data is not null;
}
