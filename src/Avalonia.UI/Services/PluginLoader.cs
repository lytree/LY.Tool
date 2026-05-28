using System.Collections.Immutable;
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
    private readonly ReaderWriterLockSlim _lock = new();
    private ImmutableList<PluginInfo>? _cachedPluginList;

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
        _lock.EnterReadLock();
        try
        {
            return _cachedPluginList ??= _entries.Values
                .Select(e => e.Info)
                .ToImmutableList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public PluginInfo? GetPlugin(string pluginId)
    {
        _lock.EnterReadLock();
        try
        {
            return _entries.TryGetValue(pluginId, out var entry) ? entry.Info : null;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public PluginLoadResult LoadPlugin(PluginInfo pluginInfo)
    {
        _lock.EnterWriteLock();
        try
        {
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
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        if (!File.Exists(pluginInfo.AssemblyPath))
        {
            _lock.EnterWriteLock();
            try
            {
                pluginInfo.State = PluginState.Error;
                pluginInfo.ErrorMessage = $"Assembly not found: {pluginInfo.AssemblyPath}";
                SavePluginManifest(pluginInfo);
                InvalidateSnapshot();
                PluginStateChanged?.Invoke(this, pluginInfo);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
            return new PluginLoadResult { Success = false, ErrorMessage = pluginInfo.ErrorMessage };
        }

        _lock.EnterReadLock();
        try
        {
            if (!ValidateDependencies(pluginInfo, out var depError))
            {
                _lock.ExitReadLock();
                _lock.EnterWriteLock();
                try
                {
                    pluginInfo.State = PluginState.Error;
                    pluginInfo.ErrorMessage = depError;
                    SavePluginManifest(pluginInfo);
                    InvalidateSnapshot();
                    PluginStateChanged?.Invoke(this, pluginInfo);
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
                return new PluginLoadResult { Success = false, ErrorMessage = depError };
            }
        }
        finally
        {
            if (_lock.IsReadLockHeld)
                _lock.ExitReadLock();
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
            _lock.EnterWriteLock();
            try
            {
                pluginInfo.State = PluginState.Error;
                pluginInfo.ErrorMessage = $"Failed to load plugin: {ex.Message}";
                SavePluginManifest(pluginInfo);
                InvalidateSnapshot();
                PluginStateChanged?.Invoke(this, pluginInfo);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
            return new PluginLoadResult { Success = false, ErrorMessage = pluginInfo.ErrorMessage };
        }

        if (plugin == null)
        {
            loadContext.Unload();
            _lock.EnterWriteLock();
            try
            {
                pluginInfo.State = PluginState.Error;
                pluginInfo.ErrorMessage = "No IPlugin implementation found in assembly";
                SavePluginManifest(pluginInfo);
                InvalidateSnapshot();
                PluginStateChanged?.Invoke(this, pluginInfo);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
            return new PluginLoadResult { Success = false, ErrorMessage = pluginInfo.ErrorMessage };
        }

        _lock.EnterWriteLock();
        try
        {
            var entry = GetOrCreateEntry(pluginInfo.PluginId);
            entry.Context = loadContext;
            entry.Plugin = plugin;
            if (metadata != null)
            {
                entry.Metadata = metadata;
                pluginInfo.HasMetadata = true;
            }

            pluginInfo.State = PluginState.Loaded;
            pluginInfo.ErrorMessage = null;
            SavePluginManifest(pluginInfo);
            InvalidateSnapshot();

            PluginLoaded?.Invoke(this, pluginInfo);
            PluginStateChanged?.Invoke(this, pluginInfo);

            return new PluginLoadResult { Success = true, Plugin = plugin, Metadata = metadata };
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void UnloadPlugin(string pluginId)
    {
        _lock.EnterWriteLock();
        try
        {
            if (!_entries.TryGetValue(pluginId, out var entry) || entry.Plugin is null) return;

            UnloadPluginCore(entry);

            if (entry.Info is not null)
            {
                entry.Info.State = PluginState.Installed;
                SavePluginManifest(entry.Info);
                InvalidateSnapshot();
                PluginUnloaded?.Invoke(this, entry.Info);
                PluginStateChanged?.Invoke(this, entry.Info);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void DisablePlugin(string pluginId)
    {
        _lock.EnterWriteLock();
        try
        {
            if (!_entries.TryGetValue(pluginId, out var entry)) return;

            if (entry.Plugin is not null)
            {
                UnloadPluginCore(entry);
            }

            entry.Info.State = PluginState.Disabled;
            entry.Info.ErrorMessage = null;
            SavePluginManifest(entry.Info);
            InvalidateSnapshot();
            PluginUnloaded?.Invoke(this, entry.Info);
            PluginStateChanged?.Invoke(this, entry.Info);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void EnablePlugin(string pluginId)
    {
        PluginInfo info;
        _lock.EnterWriteLock();
        try
        {
            if (!_entries.TryGetValue(pluginId, out var entry)) return;
            if (entry.Info.State != PluginState.Disabled) return;

            entry.Info.State = PluginState.Installed;
            SavePluginManifest(entry.Info);
            InvalidateSnapshot();
            PluginStateChanged?.Invoke(this, entry.Info);
            info = entry.Info;
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        LoadPlugin(info);
    }

    public void MarkForUninstall(string pluginId)
    {
        _lock.EnterWriteLock();
        try
        {
            if (!_entries.TryGetValue(pluginId, out var entry)) return;
            if (entry.Info.IsBuiltIn) return;

            if (entry.Plugin is not null)
            {
                UnloadPluginCore(entry);
            }

            entry.Info.State = PluginState.PendingUninstall;
            entry.Info.ErrorMessage = null;
            SavePluginManifest(entry.Info);
            InvalidateSnapshot();
            PluginUnloaded?.Invoke(this, entry.Info);
            PluginStateChanged?.Invoke(this, entry.Info);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void LoadAllPlugins()
    {
        List<PluginInfo> toLoad;
        _lock.EnterReadLock();
        try
        {
            toLoad = _entries.Values
                .Where(e => e.Info.State == PluginState.Installed || e.Info.State == PluginState.Error)
                .Select(e => e.Info)
                .ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }

        foreach (var info in toLoad)
        {
            LoadPlugin(info);
        }

        _lock.EnterWriteLock();
        try
        {
            LoadExtraPlugins();
        }
        finally
        {
            _lock.ExitWriteLock();
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

            if (_entries.TryGetValue(pluginId, out var existing) && existing.Plugin is not null)
                return;

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

            var entry = GetOrCreateEntry(pluginId);
            entry.Info = pluginInfo;
            InvalidateSnapshot();
            LoadPlugin(pluginInfo);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load extra plugin from '{dllPath}': {ex.Message}");
        }
    }

    public void RegisterPlugin(PluginInfo pluginInfo)
    {
        _lock.EnterWriteLock();
        try
        {
            var entry = GetOrCreateEntry(pluginInfo.PluginId);
            entry.Info = pluginInfo;
            SavePluginManifest(pluginInfo);
            InvalidateSnapshot();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void UnregisterPlugin(string pluginId)
    {
        _lock.EnterWriteLock();
        try
        {
            if (_entries.TryGetValue(pluginId, out var entry) && entry.Plugin is not null)
            {
                UnloadPluginCore(entry);
            }
            _entries.Remove(pluginId);
            InvalidateSnapshot();
            DeletePluginManifest(pluginId);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public IPlugin? GetLoadedPlugin(string pluginId)
    {
        _lock.EnterReadLock();
        try
        {
            return _entries.TryGetValue(pluginId, out var entry) ? entry.Plugin : null;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public IPluginMetadata? GetLoadedMetadata(string pluginId)
    {
        _lock.EnterReadLock();
        try
        {
            return _entries.TryGetValue(pluginId, out var entry) ? entry.Metadata : null;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private void UnloadPluginCore(PluginEntry entry)
    {
        entry.Plugin = null;
        entry.Metadata = null;

        if (entry.Context is not null)
        {
            try { entry.Context.Unload(); } catch { }
            entry.Context = null;
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
                    pluginInfo.State = PluginState.Installed;
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
                pluginInfo.InstallPath = pluginDir;
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
        _lock.EnterWriteLock();
        try
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
        finally
        {
            _lock.ExitWriteLock();
        }
        _lock.Dispose();
    }

    private sealed class PluginEntry
    {
        public PluginInfo Info { get; set; } = new();
        public IPlugin? Plugin { get; set; }
        public IPluginMetadata? Metadata { get; set; }
        public AssemblyLoadContext? Context { get; set; }
    }

    private class PluginManifest
    {
        public string? PluginId { get; set; }
        public string? Name { get; set; }
        public string? Version { get; set; }
        public string? Author { get; set; }
        public string? Description { get; set; }
        public string? Assembly { get; set; }
        public List<string>? Dependencies { get; set; }
        public string? State { get; set; }
        public DateTime? InstallTime { get; set; }
        public bool IsBuiltIn { get; set; }
    }
}
