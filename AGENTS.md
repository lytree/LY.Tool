# AvaloniaTemplate — AGENTS.md

Compact guidance for OpenCode agents working in this repository.

## Build & Run

- **Build system**: Cake Frosting (`build/build.cs` — .NET 10 file-based app, Cake 6.1.0). Call via `.\build.ps1` (Windows) or `./build.sh` (Linux/macOS).
  ```
  .\build.ps1 --build=all                    # default: bin + nuget + plugin
  .\build.ps1 --build=bin                    # desktop launcher only
  .\build.ps1 --build=nuget                  # pack NuGet packages (Generators + Shared)
  .\build.ps1 --build=plugin                 # build & zip all plugins
  .\build.ps1 --configuration=Debug          # override config (default: Release)
  .\build.ps1 --package-version=1.2.3        # set version (default: 1.0.0)
  .\build.ps1 --runtime-identifier=win-x64   # set RID for launcher publish
  .\build.ps1 --self-contained=true          # self-contained launcher publish
  .\build.ps1 --nuget-api-key=<KEY>          # push packages to nuget.org
  ```
- **Build order matters**: `--build=nuget` must run first before `--build=plugin` (or use `--build=all`), because plugins depend on `Avalonia.Plugin.Generators` + `Avalonia.Plugin.Shared` locally built NuGet packages.
- **Direct `dotnet build`** works for individual projects, but plugins may fail to restore without the local NuGet packages pre-built (use `--build=nuget` or ensure `bin/nuget/` has the `.nupkg` files).
- **Run launcher**: `dotnet run --project src/launcher/Avalonia.Launcher.Desktop`
- **VS Code debug**: Use the "Debug Plugin - {Name}" launch configs — each sets `AVALONIA_EXTRA_PLUGINS_PATH` to the plugin's `bin/Debug/net10.0` output for live dev loading.
- **No tests**, no CI workflows, no linters/formatters configured.

## Architecture

### Two solutions
| Solution | Contents |
|----------|----------|
| `Core.slnx` | Host: Generators, Shared, UI, Launcher, Platforms.Abstractions |
| `Plugins.slnx` | Generators, Shared, all `plugins/*` projects (10 plugins) |

### Project layers (src/)
```
Avalonia.Plugin.Generators/        Roslyn incremental source generator (netstandard2.1, IsRoslynComponent)
Avalonia.Plugin.Shared/            Shared contracts: IPlugin, IPluginMetadata, ViewLocator, ServiceLocator, attributes, controls
Avalonia.Platforms.Abstractions/   Cross-platform abstraction base classes (empty README only)
Avalonia.UI/                       Host app: ViewModels, Views, Services (EF Core, navigation, menu, localization, ZLogger)
Avalonia.Launcher.Desktop/         Desktop entry point (Program.cs → App.axaml.cs). Sets AvaloniaUseCompiledBindingsByDefault=true.
```

### Platform-specific projects
`src/platforms/` contains:
- `Avalonia.Platforms.Windows` — `net10.0-windows10.0.19041.0`
- `Avalonia.Platforms.MacOs` — `net10.0-macos15.0`
- `Avalonia.Platforms.Linux` — `net10.0`

### Plugin projects (plugins/)
Each plugin is a `net10.0` library referencing `Avalonia.Plugin.Generators` (analyzer, `OutputItemType="Analyzer"`, `ReferenceOutputAssembly="false"`) and `Avalonia.Plugin.Shared` (`PrivateAssets="all"`). Plugin metadata is declared via MSBuild properties:
```xml
<PluginId>UUID</PluginId>
<PluginName>...</PluginName>
<PluginAuthor>...</PluginAuthor>
<PluginDescription>...</PluginDescription>
<PluginVersion>1.0.0</PluginVersion>  <!-- optional, falls back to <Version> -->
```

10 plugins: ButtonsInputs, DateTime, DialogFeedbacks, Downloader, LayoutDisplay, NavigationMenus, ProDataGrid, ScottPlot, TDLSharp, Template.

### App startup flow
```
Program.cs → App.Initialize()
  1. Build DI container via ServiceCollectionExtensions.AddAvaloniaServices()
  2. ServiceLocator.Initialize(provider) — static gateway for plugin code
  3. InitializeDatabase() — SQLite via EF Core (AppDbContext)
  4. InitializeLocalization() — restore saved locale
  5. LoadPluginsAsync() — discover, load, and register all plugins
  6. OnFrameworkInitializationCompleted() → show splash, then MainWindow
```

### Plugin loading & assembly exclusion
- Each plugin loads in an isolated, collectible `AssemblyLoadContext`
- Framework/shared assemblies are forwarded to the default context (exclusion list in `Avalonia.Plugin.Shared.props`/`.targets`)
- Plugins auto-generate `plugin.json` manifests via the `GeneratePluginManifest` target (from `Avalonia.Plugin.Shared.targets`)
- Discovery: scans `{AppBaseDir}/plugins/` and `AVALONIA_EXTRA_PLUGINS_PATH` env var
- Built output: `bin/plugins/{Name}/publish/` (publish directory) + `bin/plugins/zip/{Name}-{Version}.zip` (stripped of .pdb, .xml, .deps.json, .runtimeconfig.json)

## Key Patterns (don't break these)

| Pattern | What to know |
|---------|-------------|
| **ServiceLocator** | Static `IServiceProvider` wrapper for plugins. Initialized once in `App.Initialize()`. Check `TryGetService<T>()` before `GetService<T>()`. |
| **ViewLocator** | Global `IDataTemplate` using `ConditionalWeakTable` for cache (leak-free VM→View cycle). Registered in XAML — `ContentControl.Content="{Binding Content}"` auto-resolves. |
| **Navigation** | Key-based `NavigationService` + `WeakReferenceMessenger` pub/sub ("JumpTo" message). Plugins register nav items in `IPlugin.GetNavigationItems()`. |
| **Menu hierarchy** | Flat menu items with optional `parentKey`. `MenuItemTreeBuilder.BuildTree()` resolves the tree. `MenuConfigurationService` manages add/remove. |
| **Source generator** | `[GenerateMetadata]` on a class implementing `IPluginMetadata` → auto-generates `IPlugin` impl. Scans companion classes for `[ViewMap]`, `[NavigationItem]`, `[Menu]` attributes. |
| **Localization** | `ILocalizationService` stacks `.resx` `ResourceManager` instances. Plugins register theirs in `Initialize()`. |
| **Plugin lifecycle** | `NotInstalled → Installed → Loaded → Disabled → PendingUninstall`. State changes fire events for UI. |

## Package & Framework Versions

All versions centralized as MSBuild properties in `src/Directory.Packages.props`:
- Avalonia: `12.0.3` (`$(AvaloniaVersion)`)
- Irihi.Ursa: `2.0.*` (`$(IrihiUrsaVersion)`)
- CommunityToolkit.Mvvm: `8.4.2` (`$(CommunityToolkit)`)
- EF Core: `10.0.8` (`$(EfCoreVersion)`)
- Microsoft.Extensions.DI: `10.0.8` (`$(MicrosoftExtensionsDI)`)
- Microsoft.Extensions.Localization: `10.0.8`
- AvaloniaUI.DiagnosticsSupport: `2.2.1`
- ProDataGrid: `12.0.0`
- ScottPlot: `5.1.58`
- ZLogger: `2.1.0`
- Plugin NuGet packages: `Avalonia.Plugin.Generators` + `Avalonia.Plugin.Shared`, version `1.0.0`, built locally to `bin/nuget/`

## NuGet Configuration

- **Root `nuget.config`**: sets `globalPackagesFolder` to `<repo>/packages` (local cache, tracked as packages/ in `.gitignore` exception for `packages/nuget/`)
- **`plugins/nuget.config`**: inherits root config, adds `AvaloniaPluginLocal` feed pointing at `<repo>/bin/nuget` — this is how plugins resolve the locally-built `Avalonia.Plugin.Generators` and `Avalonia.Plugin.Shared` packages

## Platform Targeting

`src/Environment.props` manages platform-specific TFMs:
- Windows: `net10.0-windows10.0.19041.0` + defines `Platforms_Windows`
- macOS: `net10.0-macos15.0` + defines `Platforms_MacOs` + `SupportedOSPlatformVersion=10.15`
- Linux: `net10.0` (no platform suffix) + defines `Platforms_Linux`
- Dev mode auto-detects OS via `[System.OperatingSystem]::IsWindows()` etc.
- CI uses `PublishBuilding=true` + `PublishPlatform=windows|linux|macos`
- Release+Windows → `OutputType=WinExe`

## Installed Skills (local)

Three Avalonia/Zafiro skills in `.agents/skills/` (from `sickn33/antigravity-awesome-skills`):
- `avalonia-layout-zafiro` — XAML layout conventions
- `avalonia-viewmodels-zafiro` — ViewModel/Wizard patterns
- `avalonia-zafiro-development` — mandatory conventions and rules

Skills are active and should be used when their patterns apply.

## Plugin .csproj template (for new plugins)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <OutputType>Library</OutputType>
    <PluginId>...</PluginId>
    <PluginName>...</PluginName>
    <PluginAuthor>...</PluginAuthor>
    <PluginDescription>...</PluginDescription>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Avalonia.Plugin.Generators" Version="1.0.0"
      OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
    <PackageReference Include="Avalonia.Plugin.Shared" Version="1.0.0" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

## Gotchas

- `.slnx` format (not `.sln`) — .NET 10 XML solution format
- The build script (`build/build.cs`) discovers plugins by scanning all `*.csproj` under `plugins/` — `PluginId` etc. are read from .csproj XML
- `Core.slnx` and `Plugins.slnx` share `src/Avalonia.Plugin.Generators` and `src/Avalonia.Plugin.Shared`
- Plugin NuGet packages must be built locally before plugins can restore. Build with `.\build.ps1 --build=nuget` first; packages go to `bin/nuget/`. The `plugins/nuget.config` adds this as a local feed.
- `AvaloniaUseCompiledBindingsByDefault` is set to `true` in the launcher project — follow this convention for new plugins
- `Directory.Build.props` at `src/` imports `Environment.props` and sets default `TargetFramework=net10.0` (overridden per-platform)
- The Generators project targets `netstandard2.1` (Roslyn source generator constraint) while everything else targets `net10.0`
- No `opencode.json` or `CLAUDE.md` in the repo — this `AGENTS.md` is the sole instruction file
