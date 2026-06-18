using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Models;
using Avalonia.Plugin.Shared.Services;
using Microsoft.Extensions.DependencyInjection;

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

    #region Two-phase loading

    /// <summary>
    /// 阶段1：发现并加载所有插件程序集，创建 IPlugin 实例，但不调用 InitializeAsync。
    /// 插件状态变为 Discovered。
    /// </summary>
    public async Task DiscoverAllPluginAssembliesAsync()
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
            var result = await DiscoverPluginAssemblyAsync(info);
            if (result.Success)
            {
                PluginStateChanged?.Invoke(this, result.PluginInfo ?? info);
            }
        }

        lock (_sync)
        {
            LoadExtraPlugins();
        }
    }

    /// <summary>
    /// 阶段2：调用每个已发现插件的 InitializeAsync(IServiceCollection)，
    /// 插件在初始化时向 ServiceCollection 注册服务。
    /// 必须在 DiscoverAllPluginAssembliesAsync 之后、BuildServiceProvider 之前调用。
    /// </summary>
    public async Task InitializeAllPluginsAsync(IServiceCollection services)
    {
        List<PluginEntry> toInitialize;

        lock (_sync)
        {
            toInitialize = _entries.Values
                .Where(e => e.Info.State == PluginState.Loaded && e.Plugin is not null && !e.IsInitialized)
                .ToList();
        }

        foreach (var entry in toInitialize)
        {
            List<PluginInfo> eventsToFire = [];

            try
            {
                await entry.Plugin!.InitializeAsync(services);
            }
            catch (Exception ex)
            {
                lock (_sync)
                {
                    var errInfo = entry.Info.WithState(PluginState.Error, $"Plugin initialization failed: {ex.Message}");
                    var errEntry = GetOrCreateEntry(errInfo.PluginId);
                    errEntry.Info = errInfo;
                    SavePluginManifest(errInfo);
                    InvalidateSnapshot();
                    eventsToFire.Add(errInfo);
                }
                FireEventsOutsideLock(eventsToFire);
                continue;
            }

            lock (_sync)
            {
                entry.IsInitialized = true;
                eventsToFire.Add(entry.Info);
            }

            FireEventsOutsideLock(eventsToFire);
        }
    }

    /// <summary>
    /// 阶段3：调用每个已加载插件的 RegisterAsync()，
    /// 插件在注册时执行多语言注册、SQL 初始化等操作。
    /// 必须在 ServiceProvider 构建完成之后调用。
    /// </summary>
    public async Task RegisterAllPluginsAsync(IServiceProvider serviceProvider)
    {
        List<PluginEntry> toRegister;

        lock (_sync)
        {
            toRegister = _entries.Values
                .Where(e => e.Info.State == PluginState.Loaded && e.Plugin is not null && e.IsInitialized)
                .ToList();
        }

        foreach (var entry in toRegister)
        {
            List<PluginInfo> eventsToFire = [];

            try
            {
                await entry.Plugin!.RegisterAsync(serviceProvider);
            }
            catch (Exception ex)
            {
                lock (_sync)
                {
                    var errInfo = entry.Info.WithState(PluginState.Error, $"Plugin registration failed: {ex.Message}");
                    var errEntry = GetOrCreateEntry(errInfo.PluginId);
                    errEntry.Info = errInfo;
                    SavePluginManifest(errInfo);
                    InvalidateSnapshot();
                    eventsToFire.Add(errInfo);
                }
                FireEventsOutsideLock(eventsToFire);
                continue;
            }

            eventsToFire.Add(entry.Info);
            FireEventsOutsideLock(eventsToFire);
        }
    }

    /// <summary>
    /// 发现单个插件程序集：加载 Assembly，创建 IPlugin/IPluginMetadata 实例，状态设为 Loaded。
    /// </summary>
    private async Task<PluginLoadResult> DiscoverPluginAssemblyAsync(PluginInfo pluginInfo)
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
            entry.IsInitialized = false;
            SavePluginManifest(pluginInfo);
            InvalidateSnapshot();

            return new PluginLoadResult { Success = true, Plugin = plugin, Metadata = metadata, PluginInfo = pluginInfo };
        }
    }

    #endregion

    #region Legacy full-load (kept for IPluginLoader compatibility)

    /// <summary>
    /// IPluginLoader 显式实现：完整加载流程（不带 ServiceCollection）。
    /// </summary>
    Task IPluginLoader.LoadAllPluginsAsync() => LoadAllPluginsAsync(null);

    /// <summary>
    /// IPluginLoader 显式实现：完整加载单个插件（不带 ServiceCollection）。
    /// </summary>
    Task<PluginLoadResult> IPluginLoader.LoadPluginAsync(PluginInfo pluginInfo) => LoadPluginAsync(pluginInfo, null);

    /// <summary>
    /// 完整加载流程：发现 + 初始化 + 注册。
    /// </summary>
    public async Task LoadAllPluginsAsync(IServiceCollection? services = null)
    {
        await DiscoverAllPluginAssembliesAsync();

        if (services is not null)
        {
            await InitializeAllPluginsAsync(services);
        }

        // 触发 PluginLoaded 事件
        List<PluginInfo> loaded;
        lock (_sync)
        {
            loaded = _entries.Values
                .Where(e => e.Info.State == PluginState.Loaded)
                .Select(e => e.Info)
                .ToList();
        }

        foreach (var info in loaded)
        {
            PluginLoaded?.Invoke(this, info);
        }
    }

    /// <summary>
    /// 完整加载单个插件：发现 + 初始化 + 注册。
    /// </summary>
    public async Task<PluginLoadResult> LoadPluginAsync(PluginInfo pluginInfo, IServiceCollection? services = null)
    {
        var result = await DiscoverPluginAssemblyAsync(pluginInfo);
        if (!result.Success) return result;

        // 立即初始化
        var discoveredPlugin = result.Plugin;
        if (discoveredPlugin == null) return result;

        List<PluginInfo> eventsToFire = [];

        try
        {
            await discoveredPlugin.InitializeAsync(services ?? new ServiceCollection());
        }
        catch (Exception ex)
        {
            lock (_sync)
            {
                var errInfo = pluginInfo.WithState(PluginState.Error, $"Plugin initialization failed: {ex.Message}");
                UpdateEntry(errInfo);
                SavePluginManifest(errInfo);
                InvalidateSnapshot();
                eventsToFire.Add(errInfo);
            }
            FireEventsOutsideLock(eventsToFire);
            return new PluginLoadResult { Success = false, ErrorMessage = $"Plugin initialization failed: {ex.Message}" };
        }

        lock (_sync)
        {
            var entry = GetOrCreateEntry(pluginInfo.PluginId);
            entry.IsInitialized = true;

            return new PluginLoadResult { Success = true, Plugin = discoveredPlugin, Metadata = result.Metadata };
        }
    }

    #endregion

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

    private void FireEventsOutsideLock(List<PluginInfo> events)
    {
        foreach (var info in events)
        {
            PluginStateChanged?.Invoke(this, info);
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

            DiscoverPluginAssemblyAsync(pluginInfo).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load extra plugin from '{dllPath}': {ex.Message}");
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
        public bool IsInitialized { get; set; }
    }
}
