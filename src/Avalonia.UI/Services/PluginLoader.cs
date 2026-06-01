using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Models;
using Avalonia.Plugin.Shared.Services;

namespace Avalonia.UI.Services;

public class PluginLoader : IPluginLoader, IDisposable
{
    public const string ExtraPluginEnvironmentVariableName = "AVALONIA_EXTRA_PLUGINS_PATH";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly Dictionary<string, PluginEntry> _entries = [];
    private readonly string _pluginsDirectory;
    private readonly string? _extraPluginPath;
    private readonly object _sync = new();
    private List<PluginInfo>? _cachedPluginList;

    public event EventHandler<PluginInfo>? PluginLoaded;
    public event EventHandler<PluginInfo>? PluginUnloaded;
    public event EventHandler<PluginInfo>? PluginStateChanged;

    public PluginLoader(string? pluginsDirectory = null)
    {
        _pluginsDirectory = pluginsDirectory ?? Path.Combine(AppContext.BaseDirectory, "plugins");
        _extraPluginPath = Environment.GetEnvironmentVariable(ExtraPluginEnvironmentVariableName);
        Directory.CreateDirectory(_pluginsDirectory);
        ProcessPendingUninstalls();
        LoadAllPluginManifests();
    }

    public IReadOnlyList<PluginInfo> GetInstalledPlugins()
    {
        lock (_sync)
        {
            return _cachedPluginList ??= _entries.Values.Select(e => e.Info).ToList();
        }
    }

    public PluginInfo? GetPlugin(string pluginId)
    {
        lock (_sync)
        {
            return _entries.TryGetValue(pluginId, out var entry) ? entry.Info : null;
        }
    }

    public async Task<PluginLoadResult> LoadPluginAsync(PluginInfo pluginInfo)
    {
        List<PluginInfo> eventsToFire = [];

        bool entryExisted;
        lock (_sync)
        {
            entryExisted = _entries.ContainsKey(pluginInfo.PluginId);

            if (_entries.TryGetValue(pluginInfo.PluginId, out var existing) && existing.Plugin is not null)
            {
                return new PluginLoadResult
                {
                    Success = true,
                    Plugin = existing.Plugin,
                    Metadata = existing.Metadata
                };
            }

            if (pluginInfo.State == PluginState.Disabled || pluginInfo.State == PluginState.PendingUninstall)
            {
                return new PluginLoadResult
                {
                    Success = false,
                    ErrorMessage = $"Plugin is {pluginInfo.State}, cannot load"
                };
            }

            if (!File.Exists(pluginInfo.AssemblyPath))
            {
                var errInfo = pluginInfo.WithState(PluginState.Error, $"Assembly not found: {pluginInfo.AssemblyPath}");
                if (entryExisted)
                {
                    UpdateEntry(errInfo);
                    SavePluginManifest(errInfo);
                    InvalidateSnapshot();
                    eventsToFire.Add(errInfo);
                }
                FireEventsOutsideLock(eventsToFire);
                return new PluginLoadResult { Success = false, ErrorMessage = errInfo.ErrorMessage };
            }

            if (!ValidateDependencies(pluginInfo, out var depError))
            {
                var errInfo = pluginInfo.WithState(PluginState.Error, depError);
                if (entryExisted)
                {
                    UpdateEntry(errInfo);
                    SavePluginManifest(errInfo);
                    InvalidateSnapshot();
                    eventsToFire.Add(errInfo);
                }
                FireEventsOutsideLock(eventsToFire);
                return new PluginLoadResult { Success = false, ErrorMessage = depError };
            }
        }

        AssemblyLoadContext loadContext;
        IPlugin? plugin = null;
        IPluginMetadata? metadata = null;

        try
        {
            loadContext = new PluginLoadContext(pluginInfo.AssemblyPath);
            var assembly = loadContext.LoadFromAssemblyPath(pluginInfo.AssemblyPath);

            foreach (var type in assembly.GetExportedTypes())
            {
                if (type.IsAbstract || type.IsInterface) continue;

                if (typeof(IPlugin).IsAssignableFrom(type) && plugin == null)
                {
                    plugin = (IPlugin)Activator.CreateInstance(type)!;
                }

                if (typeof(IPluginMetadata).IsAssignableFrom(type) && metadata == null)
                {
                    metadata = (IPluginMetadata)Activator.CreateInstance(type)!;
                    metadata.Initialize();
                }

                if (plugin != null && metadata != null) break;
            }
        }
        catch (Exception ex)
        {
            lock (_sync)
            {
                var errInfo = pluginInfo.WithState(PluginState.Error, $"Failed to load plugin: {ex.Message}");
                if (entryExisted)
                {
                    UpdateEntry(errInfo);
                    SavePluginManifest(errInfo);
                    InvalidateSnapshot();
                    eventsToFire.Add(errInfo);
                }
            }
            FireEventsOutsideLock(eventsToFire);
            return new PluginLoadResult { Success = false, ErrorMessage = pluginInfo.ErrorMessage };
        }

        if (plugin == null)
        {
            loadContext.Unload();
            PluginInfo errInfo;
            lock (_sync)
            {
                errInfo = pluginInfo.WithState(PluginState.Error, "No IPlugin implementation found in assembly");
                if (entryExisted)
                {
                    UpdateEntry(errInfo);
                    SavePluginManifest(errInfo);
                    InvalidateSnapshot();
                    eventsToFire.Add(errInfo);
                }
            }
            FireEventsOutsideLock(eventsToFire);
            return new PluginLoadResult { Success = false, ErrorMessage = errInfo.ErrorMessage };
        }

        try
        {
            await plugin.InitializeAsync();
        }
        catch (Exception ex)
        {
            loadContext.Unload();
            PluginInfo errInfo;
            lock (_sync)
            {
                errInfo = pluginInfo.WithState(PluginState.Error, $"Plugin initialization failed: {ex.Message}");
                UpdateEntry(errInfo);
                SavePluginManifest(errInfo);
                InvalidateSnapshot();
                eventsToFire.Add(errInfo);
            }
            FireEventsOutsideLock(eventsToFire);
            return new PluginLoadResult { Success = false, ErrorMessage = errInfo.ErrorMessage };
        }

        lock (_sync)
        {
            var entry = GetOrCreateEntry(pluginInfo.PluginId);
            entry.Context = loadContext;
            entry.Plugin = plugin;
            if (metadata != null)
            {
                entry.Metadata = metadata;
                pluginInfo = pluginInfo.WithMetadata(true);
            }

            pluginInfo = pluginInfo.WithState(PluginState.Loaded);
            entry.Info = pluginInfo;
            SavePluginManifest(pluginInfo);
            InvalidateSnapshot();

            return new PluginLoadResult { Success = true, Plugin = plugin, Metadata = metadata };
        }
    }

    public void DisablePlugin(string pluginId)
    {
        PluginInfo? info = null;

        lock (_sync)
        {
            if (!_entries.TryGetValue(pluginId, out var entry)) return;

            info = entry.Info.WithState(PluginState.Disabled);
            entry.Info = info;
            SavePluginManifest(info);
            InvalidateSnapshot();
        }

        PluginUnloaded?.Invoke(this, info);
        PluginStateChanged?.Invoke(this, info);
    }

    public void EnablePlugin(string pluginId)
    {
        PluginInfo? info = null;

        lock (_sync)
        {
            if (!_entries.TryGetValue(pluginId, out var entry)) return;
            if (entry.Info.State != PluginState.Disabled) return;

            info = entry.Info.WithState(PluginState.Installed);
            entry.Info = info;
            SavePluginManifest(info);
            InvalidateSnapshot();
        }

        if (info is not null)
        {
            PluginStateChanged?.Invoke(this, info);
            LoadPluginAsync(info).GetAwaiter().GetResult();
        }
    }

    public void MarkForUninstall(string pluginId)
    {
        PluginInfo? info = null;

        lock (_sync)
        {
            if (!_entries.TryGetValue(pluginId, out var entry)) return;
            if (entry.Info.IsBuiltIn) return;

            info = entry.Info.WithState(PluginState.PendingUninstall);
            entry.Info = info;
            SavePluginManifest(info);
            InvalidateSnapshot();
        }

        if (info is not null)
        {
            PluginUnloaded?.Invoke(this, info);
            PluginStateChanged?.Invoke(this, info);
        }
    }

    public async Task LoadAllPluginsAsync()
    {
        List<PluginInfo> toLoad;

        lock (_sync)
        {
            toLoad = _entries.Values
                .Where(e => e.Info.State == PluginState.Installed)
                .Select(e => e.Info)
                .ToList();
        }

        foreach (var info in toLoad)
        {
            var result = await LoadPluginAsync(info);
            if (result.Success)
            {
                PluginLoaded?.Invoke(this, info);
                PluginStateChanged?.Invoke(this, info);
            }
        }

        lock (_sync)
        {
            LoadExtraPlugins();
        }
    }

    private void LoadExtraPlugins()
    {
        if (string.IsNullOrWhiteSpace(_extraPluginPath) || !Directory.Exists(_extraPluginPath))
            return;

        foreach (var dllPath in Directory.GetFiles(_extraPluginPath, "*.dll", SearchOption.TopDirectoryOnly))
        {
            TryLoadExtraPluginDll(dllPath);
        }

        foreach (var subDir in Directory.GetDirectories(_extraPluginPath))
        {
            var dirName = Path.GetFileName(subDir);
            var candidateDll = Path.Combine(subDir, $"{dirName}.dll");
            if (File.Exists(candidateDll))
            {
                TryLoadExtraPluginDll(candidateDll);
            }
        }
    }

    private void TryLoadExtraPluginDll(string dllPath)
    {
        try
        {
            var assemblyName = AssemblyName.GetAssemblyName(dllPath);
            var pluginId = assemblyName.Name ?? Path.GetFileNameWithoutExtension(dllPath);

            lock (_sync)
            {
                if (_entries.TryGetValue(pluginId, out var existing) && existing.Plugin is not null)
                    return;
            }

            var pluginInfo = new PluginInfo
        {
            PluginId = pluginId,
            Name = assemblyName.Name ?? pluginId,
            Version = assemblyName.Version?.ToString() ?? "0.0.0",
            AssemblyPath = dllPath,
            InstallPath = Path.GetDirectoryName(dllPath) ?? _extraPluginPath!,
            State = PluginState.Installed,
            IsBuiltIn = false
        };

        LoadPluginAsync(pluginInfo).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load extra plugin from '{dllPath}': {ex.Message}");
        }
    }

    public void RegisterPlugin(PluginInfo pluginInfo)
    {
        lock (_sync)
        {
            UpdateEntry(pluginInfo);
            SavePluginManifest(pluginInfo);
            InvalidateSnapshot();
        }
    }

    public void UnregisterPlugin(string pluginId)
    {
        lock (_sync)
        {
            _entries.Remove(pluginId);
            InvalidateSnapshot();
            DeletePluginManifest(pluginId);
        }
    }

    public IPlugin? GetLoadedPlugin(string pluginId)
    {
        lock (_sync)
        {
            return _entries.TryGetValue(pluginId, out var entry) ? entry.Plugin : null;
        }
    }

    public IPluginMetadata? GetLoadedMetadata(string pluginId)
    {
        lock (_sync)
        {
            return _entries.TryGetValue(pluginId, out var entry) ? entry.Metadata : null;
        }
    }

    private PluginEntry GetOrCreateEntry(string pluginId)
    {
        if (!_entries.TryGetValue(pluginId, out var entry))
        {
            entry = new PluginEntry { Info = new PluginInfo { PluginId = pluginId } };
            _entries[pluginId] = entry;
        }
        return entry;
    }

    private void UpdateEntry(PluginInfo pluginInfo)
    {
        var entry = GetOrCreateEntry(pluginInfo.PluginId);
        entry.Info = pluginInfo;
    }

    private void InvalidateSnapshot()
    {
        _cachedPluginList = null;
    }

    private bool ValidateDependencies(PluginInfo pluginInfo, out string? error)
    {
        foreach (var depId in pluginInfo.Dependencies)
        {
            if (!_entries.TryGetValue(depId, out var depEntry))
            {
                error = $"Missing dependency: {depId}";
                return false;
            }

            if (depEntry.Info.State != PluginState.Loaded)
            {
                error = $"Dependency not loaded: {depId} ({depEntry.Info.Name})";
                return false;
            }
        }

        error = null;
        return true;
    }

    private void FireEventsOutsideLock(List<PluginInfo> events)
    {
        foreach (var info in events)
        {
            PluginStateChanged?.Invoke(this, info);
        }
    }

    private void ProcessPendingUninstalls()
    {
        if (!Directory.Exists(_pluginsDirectory)) return;

        foreach (var pluginDir in Directory.GetDirectories(_pluginsDirectory))
        {
            var manifestPath = Path.Combine(pluginDir, "plugin.json");
            if (!File.Exists(manifestPath)) continue;

            try
            {
                var json = File.ReadAllText(manifestPath);
                var manifest = JsonSerializer.Deserialize<PluginManifest>(json, JsonOptions);
                if (manifest == null) continue;

                if (manifest.State == nameof(PluginState.PendingUninstall))
                {
                    try
                    {
                        Directory.Delete(pluginDir, true);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to delete plugin directory '{pluginDir}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to process plugin manifest '{manifestPath}': {ex.Message}");
            }
        }
    }

    private void LoadAllPluginManifests()
    {
        if (!Directory.Exists(_pluginsDirectory)) return;

        foreach (var pluginDir in Directory.GetDirectories(_pluginsDirectory))
        {
            var manifestPath = Path.Combine(pluginDir, "plugin.json");
            if (!File.Exists(manifestPath)) continue;

            try
            {
                var json = File.ReadAllText(manifestPath);
                var manifest = JsonSerializer.Deserialize<PluginManifest>(json, JsonOptions);
                if (manifest == null) continue;

                var pluginInfo = ManifestToPluginInfo(manifest, pluginDir);

                if (pluginInfo.State == PluginState.Loaded)
                {
                    pluginInfo = pluginInfo.WithState(PluginState.Installed);
                }

                var entry = GetOrCreateEntry(pluginInfo.PluginId);
                entry.Info = pluginInfo;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load plugin manifest '{manifestPath}': {ex.Message}");
            }
        }
    }

    private PluginInfo ManifestToPluginInfo(PluginManifest manifest, string pluginDir)
    {
        var assemblyPath = !string.IsNullOrEmpty(manifest.Assembly)
            ? Path.Combine(pluginDir, manifest.Assembly)
            : string.Empty;

        return new PluginInfo
        {
            PluginId = manifest.PluginId ?? string.Empty,
            Name = manifest.Name ?? string.Empty,
            Version = manifest.Version ?? "1.0.0",
            Author = manifest.Author ?? string.Empty,
            Description = manifest.Description ?? string.Empty,
            Dependencies = manifest.Dependencies ?? [],
            InstallPath = pluginDir,
            AssemblyPath = assemblyPath,
            State = Enum.TryParse<PluginState>(manifest.State, out var state) ? state : PluginState.Installed,
            InstallTime = manifest.InstallTime,
            IsBuiltIn = manifest.IsBuiltIn,
            HasMetadata = !string.IsNullOrEmpty(manifest.PluginId)
        };
    }

    private void SavePluginManifest(PluginInfo pluginInfo)
    {
        try
        {
            var pluginDir = pluginInfo.InstallPath;
            if (string.IsNullOrEmpty(pluginDir))
            {
                pluginDir = Path.Combine(_pluginsDirectory, pluginInfo.PluginId);
                var updated = pluginInfo.WithInstallPath(pluginDir);
                var entry = GetOrCreateEntry(pluginInfo.PluginId);
                entry.Info = updated;
                pluginInfo = updated;
            }

            Directory.CreateDirectory(pluginDir);

            var manifest = new PluginManifest
            {
                PluginId = pluginInfo.PluginId,
                Name = pluginInfo.Name,
                Version = pluginInfo.Version,
                Author = pluginInfo.Author,
                Description = pluginInfo.Description,
                Assembly = !string.IsNullOrEmpty(pluginInfo.AssemblyPath)
                    ? Path.GetFileName(pluginInfo.AssemblyPath)
                    : $"{pluginInfo.Name}.dll",
                Dependencies = pluginInfo.Dependencies,
                State = pluginInfo.State.ToString(),
                InstallTime = pluginInfo.InstallTime,
                IsBuiltIn = pluginInfo.IsBuiltIn
            };

            var manifestPath = Path.Combine(pluginDir, "plugin.json");
            var json = JsonSerializer.Serialize(manifest, JsonOptions);
            File.WriteAllText(manifestPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save plugin manifest for '{pluginInfo.PluginId}': {ex.Message}");
        }
    }

    private void DeletePluginManifest(string pluginId)
    {
        if (!_entries.TryGetValue(pluginId, out var entry)) return;

        try
        {
            var manifestPath = Path.Combine(entry.Info.InstallPath, "plugin.json");
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to delete plugin manifest for '{pluginId}': {ex.Message}");
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            foreach (var entry in _entries.Values)
            {
                if (entry.Context is not null)
                {
                    try { entry.Context.Unload(); } catch { }
                }
            }
            _entries.Clear();
            _cachedPluginList = null;
        }
    }

    private sealed class PluginEntry
    {
        public PluginInfo Info { get; set; } = new();
        public IPlugin? Plugin { get; set; }
        public IPluginMetadata? Metadata { get; set; }
        public AssemblyLoadContext? Context { get; set; }
    }
}
