#!/usr/bin/env dotnet
#:package Cake.Frosting@6.1.0
#:package Cake.Common@6.1.0
#:property PublishAot=false

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Cake.Common;
using Cake.Common.IO;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Build;
using Cake.Common.Tools.DotNet.NuGet.Push;
using Cake.Common.Tools.DotNet.Pack;
using Cake.Common.Tools.DotNet.Publish;
using Cake.Common.Tools.DotNet.MSBuild;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Frosting;

return new CakeHost()
    .UseContext<BuildContext>()
    .Run(args);

[Flags]
public enum BuildTarget
{
    None = 0,
    Bin = 1,
    NuGet = 2,
    Plugin = 4,
    All = Bin | NuGet | Plugin
}

public class BuildContext : FrostingContext
{
    public BuildTarget Target { get; }
    public string BuildConfiguration { get; }
    public string PackageVersion { get; }
    public string NuGetSource { get; }
    public string NuGetApiKey { get; }
    public string RuntimeIdentifier { get; }
    public bool SelfContained { get; }

    public string RootDir { get; }
    public string PackagesDir { get; }
    public string NuGetPackagesDir { get; }
    public string BinPackagesDir { get; }
    public string PluginPackagesDir { get; }

    public string GeneratorsProject { get; }
    public string SharedProject { get; }
    public string LauncherProject { get; }
    public IReadOnlyList<PluginProjectInfo> PluginProjects { get; }

    public DotNetMSBuildSettings CreateMSBuildSettings()
    {
        return new DotNetMSBuildSettings()
            .SetVersion(PackageVersion)
            .SetConfiguration(BuildConfiguration)
            .WithProperty("ContinuousIntegrationBuild", "true");
    }

    public BuildContext(ICakeContext context)
        : base(context)
    {
        Target = ParseBuildTarget(context.Argument("build", "all"));
        BuildConfiguration = context.Argument("configuration", "Release");
        PackageVersion = context.Argument("package-version", "1.0.0");
        NuGetSource = context.Argument("nuget-source", "https://api.nuget.org/v3/index.json");
        NuGetApiKey = context.Argument("nuget-api-key", "");
        RuntimeIdentifier = context.Argument("runtime-identifier", "");
        SelfContained = context.Argument("self-contained", false);

        RootDir = context.Environment.WorkingDirectory.FullPath;
        PackagesDir = Path.Combine(RootDir, "packages");
        NuGetPackagesDir = Path.Combine(PackagesDir, "nuget");
        BinPackagesDir = Path.Combine(PackagesDir, "bin");
        PluginPackagesDir = Path.Combine(PackagesDir, "plugins");

        GeneratorsProject = Path.Combine(RootDir, "src", "Avalonia.Plugin.Generators", "Avalonia.Plugin.Generators.csproj");
        SharedProject = Path.Combine(RootDir, "src", "Avalonia.Plugin.Shared", "Avalonia.Plugin.Shared.csproj");
        LauncherProject = Path.Combine(RootDir, "src", "launcher", "Avalonia.Launcher.Desktop", "Avalonia.Launcher.Desktop.csproj");

        PluginProjects = DiscoverPlugins(RootDir);
    }

    private static IReadOnlyList<PluginProjectInfo> DiscoverPlugins(string rootDir)
    {
        var pluginsDir = Path.Combine(rootDir, "plugins");
        if (!Directory.Exists(pluginsDir))
            return Array.Empty<PluginProjectInfo>();

        var plugins = new List<PluginProjectInfo>();

        foreach (var csprojFile in Directory.GetFiles(pluginsDir, "*.csproj", SearchOption.AllDirectories))
        {
            var projectName = Path.GetFileNameWithoutExtension(csprojFile);

            var doc = XDocument.Load(csprojFile);

            var pluginId = doc.Descendants("PluginId").FirstOrDefault()?.Value ?? projectName;
            var pluginName = doc.Descendants("PluginName").FirstOrDefault()?.Value ?? projectName;
            var pluginAuthor = doc.Descendants("PluginAuthor").FirstOrDefault()?.Value ?? "AvaloniaPlugin";
            var pluginDescription = doc.Descendants("PluginDescription").FirstOrDefault()?.Value ?? "";
            var pluginVersion = doc.Descendants("PluginVersion").FirstOrDefault()?.Value
                             ?? doc.Descendants("Version").FirstOrDefault()?.Value
                             ?? "1.0.0";

            plugins.Add(new PluginProjectInfo(
                projectName, pluginId, pluginName, pluginVersion, pluginAuthor, pluginDescription));
        }

        return plugins;
    }

    private static BuildTarget ParseBuildTarget(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return BuildTarget.All;

        var result = BuildTarget.None;
        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            result |= part.ToLowerInvariant() switch
            {
                "all" => BuildTarget.All,
                "bin" => BuildTarget.Bin,
                "nuget" => BuildTarget.NuGet,
                "plugin" => BuildTarget.Plugin,
                _ => throw new ArgumentException($"Unknown build target: '{part}'. Valid values: all, bin, nuget, plugin")
            };
        }
        return result == BuildTarget.None ? BuildTarget.All : result;
    }
}

public record PluginProjectInfo(
    string ProjectName,
    string PluginId,
    string PluginName,
    string PluginVersion,
    string PluginAuthor,
    string PluginDescription)
{
    public string ProjectPath(string rootDir) => Path.Combine(rootDir, "plugins", ProjectName, $"{ProjectName}.csproj");
}

[TaskName("Clean")]
public sealed class CleanTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        var target = context.Target;

        if (target.HasFlag(BuildTarget.NuGet))
        {
            CleanDirectoryIfExists(context, context.NuGetPackagesDir);

            CleanDirectoryIfExists(context, Path.Combine(context.RootDir, "src", "Avalonia.Plugin.Generators", "bin"));
            CleanDirectoryIfExists(context, Path.Combine(context.RootDir, "src", "Avalonia.Plugin.Generators", "obj"));
            CleanDirectoryIfExists(context, Path.Combine(context.RootDir, "src", "Avalonia.Plugin.Shared", "bin"));
            CleanDirectoryIfExists(context, Path.Combine(context.RootDir, "src", "Avalonia.Plugin.Shared", "obj"));
        }

        if (target.HasFlag(BuildTarget.Bin))
        {
            CleanDirectoryIfExists(context, context.BinPackagesDir);

            CleanDirectoryIfExists(context, Path.Combine(context.RootDir, "src", "launcher", "Avalonia.Launcher.Desktop", "bin"));
            CleanDirectoryIfExists(context, Path.Combine(context.RootDir, "src", "launcher", "Avalonia.Launcher.Desktop", "obj"));
        }

        if (target.HasFlag(BuildTarget.Plugin))
        {
            CleanDirectoryIfExists(context, context.PluginPackagesDir);

            foreach (var plugin in context.PluginProjects)
            {
                var pluginDir = Path.GetDirectoryName(plugin.ProjectPath(context.RootDir))!;
                CleanDirectoryIfExists(context, Path.Combine(pluginDir, "bin"));
                CleanDirectoryIfExists(context, Path.Combine(pluginDir, "obj"));
            }
        }

        if (target.HasFlag(BuildTarget.All))
        {
            CleanDirectoryIfExists(context, context.PackagesDir);
        }

        context.Log.Information("Clean completed. Target: {0}", target);
    }

    private static void CleanDirectoryIfExists(BuildContext context, string dir)
    {
        if (Directory.Exists(dir))
        {
            context.CleanDirectory(dir);
        }
    }
}

[TaskName("Build")]
[IsDependentOn(typeof(CleanTask))]
public sealed class BuildTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        var msBuildSettings = context.CreateMSBuildSettings();

        context.DotNetBuild(context.GeneratorsProject, new DotNetBuildSettings
        {
            Configuration = context.BuildConfiguration,
            MSBuildSettings = msBuildSettings
        });

        context.DotNetBuild(context.SharedProject, new DotNetBuildSettings
        {
            Configuration = context.BuildConfiguration,
            MSBuildSettings = msBuildSettings
        });

        if (context.Target.HasFlag(BuildTarget.NuGet))
        {
            context.EnsureDirectoryExists(context.NuGetPackagesDir);

            context.DotNetPack(context.GeneratorsProject, new DotNetPackSettings
            {
                Configuration = context.BuildConfiguration,
                OutputDirectory = context.NuGetPackagesDir,
                NoBuild = true,
                MSBuildSettings = msBuildSettings
            });

            context.DotNetPack(context.SharedProject, new DotNetPackSettings
            {
                Configuration = context.BuildConfiguration,
                OutputDirectory = context.NuGetPackagesDir,
                NoBuild = true,
                MSBuildSettings = msBuildSettings
            });

            context.Log.Information("Plugin NuGet packages created in: {0}", context.NuGetPackagesDir);
        }

        if (context.Target.HasFlag(BuildTarget.Bin))
        {
            context.DotNetBuild(context.LauncherProject, new DotNetBuildSettings
            {
                Configuration = context.BuildConfiguration,
                MSBuildSettings = msBuildSettings
            });
        }

        if (context.Target.HasFlag(BuildTarget.Plugin))
        {
            foreach (var plugin in context.PluginProjects)
            {
                var pluginMsBuild = context.CreateMSBuildSettings()
                    .WithProperty("IsPluginProject", "true")
                    .WithProperty("PluginId", plugin.PluginId)
                    .WithProperty("PluginName", $"\"{plugin.PluginName}\"")
                    .WithProperty("PackageVersion", plugin.PluginVersion)
                    .WithProperty("PluginAuthor", plugin.PluginAuthor)
                    .WithProperty("PluginDescription", $"\"{plugin.PluginDescription}\"");

                context.DotNetBuild(plugin.ProjectPath(context.RootDir), new DotNetBuildSettings
                {
                    Configuration = context.BuildConfiguration,
                    MSBuildSettings = pluginMsBuild
                });
            }
        }

        context.Log.Information("Build completed. Target: {0}", context.Target);
    }
}

[TaskName("PackBin")]
[IsDependentOn(typeof(BuildTask))]
public sealed class PackBinTask : FrostingTask<BuildContext>
{
    public override bool ShouldRun(BuildContext context)
    {
        return context.Target.HasFlag(BuildTarget.Bin);
    }

    public override void Run(BuildContext context)
    {
        context.EnsureDirectoryExists(context.BinPackagesDir);

        var settings = new DotNetPublishSettings
        {
            Configuration = context.BuildConfiguration,
            OutputDirectory = context.BinPackagesDir,
            NoRestore = true,
            NoBuild = true,
        };

        if (!string.IsNullOrEmpty(context.RuntimeIdentifier))
        {
            settings.Runtime = context.RuntimeIdentifier;
        }

        if (context.SelfContained)
        {
            settings.SelfContained = true;
        }

        context.DotNetPublish(context.LauncherProject, settings);

        context.Log.Information("Launcher published to: {0}", context.BinPackagesDir);
    }
}

[TaskName("PackNuGet")]
[IsDependentOn(typeof(BuildTask))]
public sealed class PackNuGetTask : FrostingTask<BuildContext>
{
    public override bool ShouldRun(BuildContext context)
    {
        return context.Target.HasFlag(BuildTarget.NuGet);
    }

    public override void Run(BuildContext context)
    {
        context.EnsureDirectoryExists(context.NuGetPackagesDir);

        var msBuildSettings = context.CreateMSBuildSettings();

        context.DotNetPack(context.GeneratorsProject, new DotNetPackSettings
        {
            Configuration = context.BuildConfiguration,
            OutputDirectory = context.NuGetPackagesDir,
            NoRestore = true,
            NoBuild = true,
            MSBuildSettings = msBuildSettings
        });

        context.DotNetPack(context.SharedProject, new DotNetPackSettings
        {
            Configuration = context.BuildConfiguration,
            OutputDirectory = context.NuGetPackagesDir,
            NoRestore = true,
            NoBuild = true,
            MSBuildSettings = msBuildSettings
        });

        context.Log.Information("NuGet packages created in: {0}", context.NuGetPackagesDir);
        foreach (var pkg in context.GetFiles(Path.Combine(context.NuGetPackagesDir, "*.nupkg")))
        {
            context.Log.Information("  {0}", pkg.GetFilename());
        }
    }
}

[TaskName("LocalInstall")]
[IsDependentOn(typeof(PackNuGetTask))]
public sealed class LocalInstallTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        var localFeedName = "AvaloniaPluginLocal";
        var localFeedPath = context.NuGetPackagesDir;

        context.StartProcess("dotnet", new Cake.Core.IO.ProcessSettings
        {
            Arguments = $"nuget add source \"{localFeedPath}\" -n {localFeedName}"
        });

        context.Log.Information("Local NuGet feed '{0}' configured at: {1}", localFeedName, localFeedPath);
        context.Log.Information("To consume these packages, add the following to your nuget.config:");
        context.Log.Information("  <add key=\"{0}\" value=\"{1}\" />", localFeedName, localFeedPath);
    }
}

[TaskName("PushNuGet")]
[IsDependentOn(typeof(PackNuGetTask))]
public sealed class PushNuGetTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        if (string.IsNullOrEmpty(context.NuGetApiKey))
        {
            context.Log.Error("NuGet API key is required. Use --nuget-api-key=<KEY>");
            return;
        }

        var packages = context.GetFiles(Path.Combine(context.NuGetPackagesDir, "*.nupkg"));
        foreach (var pkg in packages)
        {
            context.Log.Information("Pushing {0}...", pkg.GetFilename());
            context.DotNetNuGetPush(pkg.FullPath, new DotNetNuGetPushSettings
            {
                Source = context.NuGetSource,
                ApiKey = context.NuGetApiKey
            });
        }

        context.Log.Information("NuGet packages pushed to: {0}", context.NuGetSource);
    }
}

[TaskName("PackPlugins")]
[IsDependentOn(typeof(BuildTask))]
public sealed class PackPluginsTask : FrostingTask<BuildContext>
{
    private static readonly HashSet<string> ExcludedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdb",
        ".xml",
    };

    public override bool ShouldRun(BuildContext context)
    {
        return context.Target.HasFlag(BuildTarget.Plugin);
    }

    public override void Run(BuildContext context)
    {
        context.EnsureDirectoryExists(context.PluginPackagesDir);

        foreach (var plugin in context.PluginProjects)
        {
            var pluginOutputDir = Path.Combine(context.PluginPackagesDir, plugin.ProjectName, "publish");
            context.EnsureDirectoryExists(pluginOutputDir);

            var pluginMsBuild = context.CreateMSBuildSettings()
                .WithProperty("IsPluginProject", "true")
                .WithProperty("PluginId", plugin.PluginId)
                .WithProperty("PluginName", $"\"{plugin.PluginName}\"")
                .WithProperty("PackageVersion", plugin.PluginVersion)
                .WithProperty("PluginAuthor", plugin.PluginAuthor)
                .WithProperty("PluginDescription", $"\"{plugin.PluginDescription}\"");

            context.DotNetPublish(plugin.ProjectPath(context.RootDir), new DotNetPublishSettings
            {
                Configuration = context.BuildConfiguration,
                OutputDirectory = pluginOutputDir,
                MSBuildSettings = pluginMsBuild
            });

            context.Log.Information("Plugin published: {0} -> {1}", plugin.ProjectName, pluginOutputDir);
        }

        PackPluginZips(context);

        context.Log.Information("All plugins published to: {0}", context.PluginPackagesDir);
    }

    private static void PackPluginZips(BuildContext context)
    {
        var zipOutputDir = Path.Combine(context.PluginPackagesDir, "zip");
        context.EnsureDirectoryExists(zipOutputDir);

        foreach (var plugin in context.PluginProjects)
        {
            var publishDir = Path.Combine(context.PluginPackagesDir, plugin.ProjectName, "publish");

            if (!Directory.Exists(publishDir))
            {
                context.Log.Warning("Publish directory not found for plugin: {0}, skipping zip packaging", plugin.ProjectName);
                continue;
            }

            EnsurePluginManifest(publishDir, plugin);

            var zipPath = Path.Combine(zipOutputDir, $"{plugin.ProjectName}-{plugin.PluginVersion}.zip");

            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            using (var zipStream = new FileStream(zipPath, FileMode.Create))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                foreach (var file in Directory.GetFiles(publishDir, "*", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(publishDir, file);
                    var fileName = Path.GetFileName(file);
                    var ext = Path.GetExtension(fileName);

                    if (fileName.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase) ||
                        fileName.EndsWith(".runtimeconfig.json", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (ExcludedExtensions.Contains(ext))
                        continue;

                    archive.CreateEntryFromFile(file, relativePath, CompressionLevel.Optimal);
                }
            }

            context.Log.Information("Plugin packed: {0} -> {1}", plugin.ProjectName, zipPath);
        }

        context.Log.Information("All plugin zip packages created in: {0}", zipOutputDir);
    }

    private static void EnsurePluginManifest(string publishDir, PluginProjectInfo plugin)
    {
        var manifestPath = Path.Combine(publishDir, "plugin.json");
        if (File.Exists(manifestPath)) return;

        var mainDll = Path.Combine(publishDir, $"{plugin.ProjectName}.dll");
        var assemblyName = plugin.ProjectName;

        if (File.Exists(mainDll))
        {
            try
            {
                var asmName = System.Reflection.AssemblyName.GetAssemblyName(mainDll);
                assemblyName = asmName.Name ?? plugin.ProjectName;
            }
            catch { }
        }

        var json = $@"{{
  ""pluginId"": ""{plugin.PluginId}"",
  ""name"": ""{plugin.PluginName}"",
  ""version"": ""{plugin.PluginVersion}"",
  ""author"": ""{plugin.PluginAuthor}"",
  ""description"": ""{plugin.PluginDescription}"",
  ""assembly"": ""{assemblyName}.dll"",
  ""dependencies"": []
}}";
        File.WriteAllText(manifestPath, json);
    }
}

[TaskName("Default")]
[IsDependentOn(typeof(PackBinTask))]
[IsDependentOn(typeof(PackNuGetTask))]
[IsDependentOn(typeof(PackPluginsTask))]
public class DefaultTask : FrostingTask<BuildContext>
{
}
