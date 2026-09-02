using System.Reflection;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Generated;

namespace LYBox.Layout.Core.Services;

internal static class GeneratedPluginModuleResolver
{
    public static IGeneratedPluginModule? Resolve(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var attributes = assembly.GetCustomAttributes<GeneratedPluginModuleAttribute>().ToArray();
        if (attributes.Length == 0)
            return null;

        if (attributes.Length != 1)
            throw new InvalidOperationException(
                $"Plugin assembly '{assembly.FullName}' declares more than one generated plugin module.");

        var moduleType = attributes[0].ModuleType;
        if (moduleType.Assembly != assembly)
            throw new InvalidOperationException(
                $"Generated plugin module '{moduleType.FullName}' must be declared in plugin assembly '{assembly.GetName().Name}'.");

        if (moduleType.IsAbstract || moduleType.IsInterface || !typeof(IGeneratedPluginModule).IsAssignableFrom(moduleType))
            throw new InvalidOperationException(
                $"Generated plugin module '{moduleType.FullName}' does not implement {nameof(IGeneratedPluginModule)}.");

        if (Activator.CreateInstance(moduleType, nonPublic: true) is not IGeneratedPluginModule module)
            throw new InvalidOperationException(
                $"Generated plugin module '{moduleType.FullName}' could not be created.");

        if (module.PluginType.Assembly != assembly || !typeof(IPlugin).IsAssignableFrom(module.PluginType))
            throw new InvalidOperationException(
                $"Generated plugin module '{moduleType.FullName}' exposes an invalid plugin type.");

        return module;
    }
}
