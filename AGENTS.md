# AvaloniaTemplate — AGENTS.md

Compact guidance for OpenCode agents working in this repository.

## Build & Run

- **Build system**: Cake Frosting (`build/Program.cs` on .NET 10). Call via `.\build.ps1` (Windows) or `./build.sh` (Linux/macOS).
  ```
  .\build.ps1 --build=all                    # default: bin + nuget + plugin
  .\build.ps1 --build=bin                    # desktop launcher only
  .\build.ps1 --build=nuget                  # pack NuGet packages (Generators + Shared)
  .\build.ps1 --build=plugin                 # build & zip all plugins
  .\build.ps1 --configuration=Debug          # override config
  .\build.ps1 --package-version=1.2.3        # set version
  ```
- **Direct `dotnet build`** works too for individual projects.
- **Run launcher**: `dotnet run --project src/launcher/Avalonia.Launcher.Desktop`
- **VS Code debug**: Use the "Debug Plugin - Template" launch config — sets `AVALONIA_EXTRA_PLUGINS_PATH` env var for dev plugin loading.
- **No tests**, no CI workflows, no linters/formatters configured.

## Architecture

### Two solutions
| Solution | Contents |
|----------|----------|
| `Core.slnx` | Host: Generators, Shared, UI, Launcher, Platforms.Abstractions |
| `Plugins.slnx` | Generators, Shared, all `plugins/*` projects |

### Project layers (src/)
```
Avalonia.Plugin.Generators/    Roslyn incremental source generator (netstandard2.1)
Avalonia.Plugin.Shared/        Shared contracts: IPlugin, IPluginMetadata, ViewLocator, ServiceLocator, attributes
Avalonia.Platforms.Abstractions/ Cross-platform abstraction base classes
Avalonia.UI/                   Host app: ViewModels, Views, Services (EF Core, navigation, menu, localization)
Avalonia.Launcher.Desktop/     Desktop entry point (Program.cs -> App.axaml.cs)
```

### Plugin projects (plugins/)
Each plugin is a `net10.0` library referencing `Avalonia.Plugin.Generators` (analyzer) and `Avalonia.Plugin.Shared` (PrivateAssets=all). Plugin metadata is declared via MSBuild properties:
```xml
<PluginId>UUID</PluginId>
<PluginName>...</PluginName>
<PluginAuthor>...</PluginAuthor>
<PluginDescription>...</PluginDescription>
```

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

### Plugin loading
- Each plugin loads in an isolated, collectible `AssemblyLoadContext`
- Framework assemblies (System.*, Microsoft.*, Avalonia.*, etc.) are forwarded to the default context
- Plugins publish `plugin.json` manifests (auto-generated during build)
- Discovery: scans `{AppBaseDir}/plugins/` and `AVALONIA_EXTRA_PLUGINS_PATH`
- Loaded DLLs are stripped of .pdb, .xml, .deps.json, .runtimeconfig.json; zipped as `{Name}-{Version}.zip`

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

All versions centralized in `src/Directory.Packages.props`:
- Avalonia: `12.0.3` (`$(AvaloniaVersion)`)
- Irihi.Ursa: `2.0.*` (`$(IrihiUrsaVersion)`)
- CommunityToolkit.Mvvm: `8.4.2`
- EF Core: `10.0.8`
- Microsoft.Extensions.DI: `10.0.8`
- ScottPlot: `5.1.58`
- ProDataGrid: `12.0.0`
- Plugin NuGet packages: `Avalonia.Plugin.Generators` and `Avalonia.Plugin.Shared` built locally

## Platform Targeting

`src/Environment.props` manages platform-specific TFMs:
- Windows: `net10.0-windows10.0.19041.0` + defines `Platforms_Windows`
- macOS: `net10.0-macos15.0` + defines `Platforms_MacOs`
- Linux: `net10.0` + defines `Platforms_Linux`
- Dev mode uses auto-detect; CI uses `PublishBuilding=true` + `PublishPlatform=windows|linux|macos`
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
- Plugin projects import `plugins/Directory.Build.targets` which references `Avalonia.Plugin.Shared.targets` from source (not NuGet) during local development
- The build script discovers plugins by scanning all `*.csproj` under `plugins/` — `PluginId` etc. read from .csproj XML
- No `opencode.json` or `CLAUDE.md` in the repo — this `AGENTS.md` is the sole instruction file
- `Core.slnx` and `Plugins.slnx` share `src/Avalonia.Plugin.Generators` and `src/Avalonia.Plugin.Shared`
- Plugin NuGet packages must be built locally before plugin projects can restore (`packages/nuget/` already contains 1.0.0 packages)
