using System.Reflection;
using System.Runtime.Loader;

namespace Avalonia.UI.Services;

internal class PluginLoadContext : AssemblyLoadContext
{
    private static readonly string[] ExcludedPrefixes =
    [
        "System.",
        "Microsoft.",
        "Avalonia.",
        "CommunityToolkit.",
        "Irihi.",
        "SQLitePCLRaw.",
    ];

    private static readonly HashSet<string> ExcludedExactNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Avalonia",
        "Ursa",
        "Semi.Avalonia",
        "Microsoft.Data.Sqlite",
        "MicroCom.Runtime",
        "System.Reactive",
        "System.Private.Uri",
        "Microsoft.Bcl.AsyncInterfaces",
        "SQLite",
        "Avalonia.Plugin.Shared",
    };

    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _pluginDirectory;

    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _pluginDirectory = Path.GetDirectoryName(pluginPath) ?? pluginPath;
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var name = assemblyName.Name ?? string.Empty;

        if (name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
        {
            return LoadSatelliteAssembly(assemblyName);
        }

        if (IsExcluded(name))
        {
            return AssemblyLoadContext.Default.LoadFromAssemblyName(assemblyName);
        }

        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        assemblyPath = ProbePluginDirectory(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        return null;
    }

    private Assembly? LoadSatelliteAssembly(AssemblyName assemblyName)
    {
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        var culture = assemblyName.CultureName;
        if (!string.IsNullOrEmpty(culture))
        {
            var dllName = $"{assemblyName.Name}.dll";
            var satellitePath = Path.Combine(_pluginDirectory, culture, dllName);
            if (File.Exists(satellitePath))
            {
                return LoadFromAssemblyPath(satellitePath);
            }
        }

        assemblyPath = ProbePluginDirectory(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        return AssemblyLoadContext.Default.LoadFromAssemblyName(assemblyName);
    }

    private static bool IsExcluded(string name)
    {
        if (ExcludedExactNames.Contains(name))
            return true;

        foreach (var prefix in ExcludedPrefixes)
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private string? ProbePluginDirectory(AssemblyName assemblyName)
    {
        var dllName = $"{assemblyName.Name}.dll";

        foreach (var dllPath in Directory.GetFiles(_pluginDirectory, dllName, SearchOption.AllDirectories))
        {
            try
            {
                var foundName = AssemblyName.GetAssemblyName(dllPath);
                if (string.Equals(foundName.Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return dllPath;
                }
            }
            catch
            {
            }
        }

        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        libraryPath = ProbeNativeLibrary(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }

    private string? ProbeNativeLibrary(string unmanagedDllName)
    {
        var rid = RuntimeIdentifier.Current;
        var nativeSubDir = Path.Combine("runtimes", rid, "native");

        var candidates = new List<string>();

        var ridDir = Path.Combine(_pluginDirectory, nativeSubDir);
        if (Directory.Exists(ridDir))
        {
            candidates.AddRange(Directory.GetFiles(ridDir));
        }

        var rootDir = _pluginDirectory;
        if (Directory.Exists(rootDir))
        {
            foreach (var f in Directory.GetFiles(rootDir))
            {
                var fn = Path.GetFileName(f);
                if (IsNativeLibraryMatch(fn, unmanagedDllName))
                    candidates.Add(f);
            }
        }

        foreach (var candidate in candidates)
        {
            var fileName = Path.GetFileName(candidate);
            if (IsNativeLibraryMatch(fileName, unmanagedDllName))
            {
                return candidate;
            }
        }

        if (Directory.Exists(Path.Combine(_pluginDirectory, "runtimes")))
        {
            foreach (var file in Directory.GetFiles(
                Path.Combine(_pluginDirectory, "runtimes"), "*", SearchOption.AllDirectories))
            {
                var fileName = Path.GetFileName(file);
                if (IsNativeLibraryMatch(fileName, unmanagedDllName))
                {
                    return file;
                }
            }
        }

        return null;
    }

    private static bool IsNativeLibraryMatch(string fileName, string unmanagedDllName)
    {
        var baseName = unmanagedDllName;

        if (string.Equals(fileName, baseName, StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(fileName, baseName + ".dll", StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(fileName, "lib" + baseName + ".so", StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(fileName, "lib" + baseName + ".dylib", StringComparison.OrdinalIgnoreCase))
            return true;

        if (fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(fileName[..^4], baseName, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static class RuntimeIdentifier
    {
        public static string Current
        {
            get
            {
                var arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture switch
                {
                    System.Runtime.InteropServices.Architecture.X64 => "x64",
                    System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
                    System.Runtime.InteropServices.Architecture.X86 => "x86",
                    System.Runtime.InteropServices.Architecture.Arm => "arm",
                    _ => "x64"
                };

                if (OperatingSystem.IsWindows()) return $"win-{arch}";
                if (OperatingSystem.IsLinux()) return $"linux-{arch}";
                if (OperatingSystem.IsMacOS()) return $"osx-{arch}";
                return $"win-{arch}";
            }
        }
    }
}
