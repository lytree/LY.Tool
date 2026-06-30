#!/usr/bin/env dotnet
#:sdk Cake.Sdk@6.2.0
#:property PublishAot=false

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
// Disambiguate System.IO types from Cake.Core.IO.Path / Cake.Common helpers
using Path = System.IO.Path;
using File = System.IO.File;
using Directory = System.IO.Directory;

//////////////////////////////////////////////////////////////////////
// ARGUMENTS / CONTEXT
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var buildContext = new BuildContext(Context);

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(c =>
{
    var t = buildContext.Target;

    // Bin 同时清理 SDK 与宿主产物
    if (t.HasFlag(BuildTarget.Bin))
    {
        CleanDirectoryIfExists(c, buildContext.NuGetPackagesDir);
        CleanDirectoryIfExists(c, buildContext.BinPackagesDir);

        CleanDirectoryIfExists(c, Path.Combine(buildContext.RootDir, "src", "Avalonia.Plugin.Generators", "bin"));
        CleanDirectoryIfExists(c, Path.Combine(buildContext.RootDir, "src", "Avalonia.Plugin.Generators", "obj"));
        CleanDirectoryIfExists(c, Path.Combine(buildContext.RootDir, "src", "Avalonia.Plugin.Shared", "bin"));
        CleanDirectoryIfExists(c, Path.Combine(buildContext.RootDir, "src", "Avalonia.Plugin.Shared", "obj"));

        CleanDirectoryIfExists(c, Path.Combine(buildContext.RootDir, "src", "launcher", "Avalonia.Launcher.Desktop", "bin"));
        CleanDirectoryIfExists(c, Path.Combine(buildContext.RootDir, "src", "launcher", "Avalonia.Launcher.Desktop", "obj"));
    }

    if (t.HasFlag(BuildTarget.Plugin))
    {
        CleanDirectoryIfExists(c, buildContext.PluginPackagesDir);

        foreach (var plugin in buildContext.PluginProjects)
        {
            var pluginDir = Path.GetDirectoryName(plugin.ProjectPath(buildContext.RootDir))!;
            CleanDirectoryIfExists(c, Path.Combine(pluginDir, "bin"));
            CleanDirectoryIfExists(c, Path.Combine(pluginDir, "obj"));
        }
    }

    if (t.HasFlag(BuildTarget.All))
    {
        CleanDirectoryIfExists(c, buildContext.PackagesDir);
    }

    c.Log.Information("Clean completed. Target: {0}", t);

    static void CleanDirectoryIfExists(ICakeContext ctx, string dir)
    {
        if (Directory.Exists(dir))
        {
            try
            {
                ctx.CleanDirectory(dir);
            }
            catch (DirectoryNotFoundException ex)
            {
                ctx.Log.Warning("CleanDirectory skipped due to inaccessible path: {0}", ex.Message);
            }
        }
    }
});

Task("Build")
    .IsDependentOn("Clean")
    .Does(c =>
{
    var hostSettings = buildContext.CreateHostMSBuildSettings();

    // Bin = SDK 编译 + NuGet 打包 + 宿主编译（统一发版）
    // 关键：NuGet pack 必须在插件 build 之前完成，因为插件 restore 依赖 bin/nuget/ 本地 feed
    if (buildContext.Target.HasFlag(BuildTarget.Bin))
    {
        // SDK 层：Generators + Shared
        c.DotNetBuild(buildContext.GeneratorsProject, new DotNetBuildSettings
        {
            Configuration = buildContext.BuildConfiguration,
            MSBuildSettings = hostSettings
        });

        c.DotNetBuild(buildContext.SharedProject, new DotNetBuildSettings
        {
            Configuration = buildContext.BuildConfiguration,
            MSBuildSettings = hostSettings
        });

        // SDK NuGet 打包（NoBuild=true 复用上一步构建结果，输出到 bin/nuget/）
        c.EnsureDirectoryExists(buildContext.NuGetPackagesDir);
        c.DotNetPack(buildContext.GeneratorsProject, new DotNetPackSettings
        {
            Configuration = buildContext.BuildConfiguration,
            OutputDirectory = buildContext.NuGetPackagesDir,
            NoRestore = true,
            NoBuild = true,
            MSBuildSettings = hostSettings
        });
        c.DotNetPack(buildContext.SharedProject, new DotNetPackSettings
        {
            Configuration = buildContext.BuildConfiguration,
            OutputDirectory = buildContext.NuGetPackagesDir,
            NoRestore = true,
            NoBuild = true,
            MSBuildSettings = hostSettings
        });
        c.Log.Information("SDK NuGet packages created in: {0}", buildContext.NuGetPackagesDir);

        // 宿主层：Launcher
        c.DotNetBuild(buildContext.LauncherProject, new DotNetBuildSettings
        {
            Configuration = buildContext.BuildConfiguration,
            MSBuildSettings = hostSettings
        });
    }

    // 插件层：各插件用自己的 PluginVersion（不再被 PackageVersion 覆盖）
    // 注：插件 restore 依赖 bin/nuget/ 本地 feed，必须等上面的 SDK pack 完成
    if (buildContext.Target.HasFlag(BuildTarget.Plugin))
    {
        foreach (var plugin in buildContext.PluginProjects)
        {
            var pluginMsBuild = buildContext.CreatePluginMSBuildSettings(plugin);

            c.DotNetBuild(plugin.ProjectPath(buildContext.RootDir), new DotNetBuildSettings
            {
                Configuration = buildContext.BuildConfiguration,
                MSBuildSettings = pluginMsBuild
            });
        }
    }

    c.Log.Information("Build completed. Target: {0}", buildContext.Target);
});

Task("PackBin")
    .IsDependentOn("Build")
    .WithCriteria(c => buildContext.Target.HasFlag(BuildTarget.Bin), "Bin target not selected")
    .Does(c =>
{
    // SDK NuGet 包已在 Build 阶段产出（必须在插件 restore 之前完成）。
    // 这里只列出已生成产物，便于发版确认。
    foreach (var pkg in c.GetFiles(Path.Combine(buildContext.NuGetPackagesDir, "*.nupkg")))
    {
        c.Log.Information("  NuGet: {0}", pkg.GetFilename());
    }

    // 发布宿主 launcher
    c.EnsureDirectoryExists(buildContext.BinPackagesDir);

    var settings = new DotNetPublishSettings
    {
        Configuration = buildContext.BuildConfiguration,
        OutputDirectory = buildContext.BinPackagesDir,
        NoRestore = true,
        NoBuild = true,
    };

    if (!string.IsNullOrEmpty(buildContext.RuntimeIdentifier))
    {
        settings.Runtime = buildContext.RuntimeIdentifier;
        settings.OutputDirectory = Path.Combine(buildContext.BinPackagesDir, buildContext.RuntimeIdentifier);
        // Build 未按 RID 编译，publish 需要重新构建 RID 产物
        settings.NoBuild = false;
        settings.NoRestore = false;
    }

    if (buildContext.SelfContained)
    {
        settings.SelfContained = true;
    }

    c.DotNetPublish(buildContext.LauncherProject, settings);

    c.Log.Information("Launcher published to: {0}", buildContext.BinPackagesDir);
});

Task("LocalInstall")
    .IsDependentOn("PackBin")
    .Does(c =>
{
    var localFeedName = "AvaloniaPluginLocal";
    var localFeedPath = buildContext.NuGetPackagesDir;

    c.StartProcess("dotnet", new Cake.Core.IO.ProcessSettings
    {
        Arguments = $"nuget add source \"{localFeedPath}\" -n {localFeedName}"
    });

    c.Log.Information("Local NuGet feed '{0}' configured at: {1}", localFeedName, localFeedPath);
    c.Log.Information("To consume these packages, add the following to your nuget.config:");
    c.Log.Information("  <add key=\"{0}\" value=\"{1}\" />", localFeedName, localFeedPath);
});

Task("PushNuGet")
    .IsDependentOn("PackBin")
    .Does(c =>
{
    if (string.IsNullOrEmpty(buildContext.NuGetApiKey))
    {
        c.Log.Error("NuGet API key is required. Use --nuget-api-key=<KEY>");
        return;
    }

    var packages = c.GetFiles(Path.Combine(buildContext.NuGetPackagesDir, "*.nupkg"));
    foreach (var pkg in packages)
    {
        c.Log.Information("Pushing {0}...", pkg.GetFilename());
        c.DotNetNuGetPush(pkg.FullPath, new DotNetNuGetPushSettings
        {
            Source = buildContext.NuGetSource,
            ApiKey = buildContext.NuGetApiKey
        });
    }

    c.Log.Information("NuGet packages pushed to: {0}", buildContext.NuGetSource);
});

Task("PackPlugins")
    .IsDependentOn("Build")
    .WithCriteria(c => buildContext.Target.HasFlag(BuildTarget.Plugin), "Plugin target not selected")
    .Does(c =>
{
    c.EnsureDirectoryExists(buildContext.PluginPackagesDir);

    foreach (var plugin in buildContext.PluginProjects)
    {
        var pluginOutputDir = Path.Combine(buildContext.PluginPackagesDir, plugin.ProjectName, "publish");
        c.EnsureDirectoryExists(pluginOutputDir);

        var pluginMsBuild = buildContext.CreatePluginMSBuildSettings(plugin);

        c.DotNetPublish(plugin.ProjectPath(buildContext.RootDir), new DotNetPublishSettings
        {
            Configuration = buildContext.BuildConfiguration,
            OutputDirectory = pluginOutputDir,
            MSBuildSettings = pluginMsBuild
        });

        c.Log.Information("Plugin published: {0} -> {1}", plugin.ProjectName, pluginOutputDir);
    }

    PackPluginZips(c, buildContext);

    c.Log.Information("All plugins published to: {0}", buildContext.PluginPackagesDir);

    static void PackPluginZips(ICakeContext ctx, BuildContext bctx)
    {
        var zipOutputDir = Path.Combine(bctx.PluginPackagesDir, "zip");
        ctx.EnsureDirectoryExists(zipOutputDir);

        foreach (var plugin in bctx.PluginProjects)
        {
            var publishDir = Path.Combine(bctx.PluginPackagesDir, plugin.ProjectName, "publish");

            if (!Directory.Exists(publishDir))
            {
                ctx.Log.Warning("Publish directory not found for plugin: {0}, skipping zip packaging", plugin.ProjectName);
                continue;
            }

            EnsurePluginManifest(publishDir, plugin, bctx, ctx);

            var effectiveVersion = bctx.GetEffectivePluginVersion(plugin);
            var zipPath = Path.Combine(zipOutputDir, $"{plugin.ProjectName}-{effectiveVersion}.zip");

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

                    // 排除调试符号、文档注释、构建配置等运行时不需要的文件
                    var extension = Path.GetExtension(file);
                    if (BuildContext.ExcludedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                    {
                        ctx.Log.Debug("Skipping excluded file: {0}", relativePath);
                        continue;
                    }

                    // 排除 .deps.json、.runtimeconfig.json 等 SDK 生成的配置
                    if (fileName.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase) ||
                        fileName.EndsWith(".runtimeconfig.json", StringComparison.OrdinalIgnoreCase))
                    {
                        ctx.Log.Debug("Skipping SDK generated config: {0}", relativePath);
                        continue;
                    }

                    var entry = archive.CreateEntry(relativePath);
                    using (var entryStream = entry.Open())
                    using (var fileStream = File.OpenRead(file))
                    {
                        fileStream.CopyTo(entryStream);
                    }
                }
            }

            ctx.Log.Information("Plugin zip created: {0}", zipPath);
        }

        ctx.Log.Information("All plugin zip packages created in: {0}", zipOutputDir);
    }

    static void EnsurePluginManifest(string publishDir, PluginProjectInfo plugin, BuildContext bctx, ICakeContext ctx)
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

        var effectiveVersion = bctx.GetEffectivePluginVersion(plugin);

        var json = $@"{{
  ""pluginId"": ""{plugin.PluginId}"",
  ""name"": ""{plugin.PluginName}"",
  ""version"": ""{effectiveVersion}"",
  ""author"": ""{plugin.PluginAuthor}"",
  ""description"": ""{plugin.PluginDescription}"",
  ""assembly"": ""{assemblyName}.dll"",
  ""dependencies"": [],
  ""minPluginSdkVersion"": ""{plugin.MinPluginSdkVersion}""
}}";
        File.WriteAllText(manifestPath, json);
    }
});

Task("Default")
    .IsDependentOn("PackBin")
    .IsDependentOn("PackPlugins");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);

//////////////////////////////////////////////////////////////////////
// SUPPORTING TYPES
//////////////////////////////////////////////////////////////////////

[Flags]
public enum BuildTarget
{
    None = 0,
    // Bin 同时构建宿主 launcher 与 SDK NuGet 包（统一发版）
    Bin = 1,
    Plugin = 4,
    All = Bin | Plugin
}

/// <summary>
/// 包装 ICakeContext，集中管理构建参数、目录解析与版本覆盖逻辑。
/// 不再继承 FrostingContext —— 符合官方 Cake.Sdk 项目设置模式。
/// </summary>
public class BuildContext
{
    // 插件 zip 排除的文件扩展名（调试符号、文档注释等运行时不需要的文件）
    public static readonly HashSet<string> ExcludedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdb",
        ".xml",
    };

    public BuildTarget Target { get; }
    public string BuildConfiguration { get; }

    // 两层独立版本覆盖：留空时由 csproj 真相源（HostVersion / PluginVersion）决定
    // 注：宿主与 SDK 已合并为同一版本号，--host-version 同时覆盖两层
    public string? HostVersionOverride { get; }
    public string? PluginVersionOverride { get; }

    // 兼容回退：显式传 --package-version 时覆盖所有层（紧急发版用）
    public string? PackageVersion { get; }

    // 插件过滤：--plugin=<Name> 只构建匹配的插件（逗号分隔多个）
    public string? PluginFilter { get; }

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

    // 宿主+SDK 版本覆盖（优先级：--host-version > --package-version > csproj 真相源 HostVersion）
    // 宿主与 SDK 共用 HostVersion，一份 settings 即可
    public DotNetMSBuildSettings CreateHostMSBuildSettings()
    {
        var settings = BaseSettings();
        if (!string.IsNullOrEmpty(HostVersionOverride))
            settings.SetVersion(HostVersionOverride);
        else if (!string.IsNullOrEmpty(PackageVersion))
            settings.SetVersion(PackageVersion);
        return settings;
    }

    // 插件版本覆盖（优先级：--plugin-version > --package-version > csproj <PluginVersion>）
    // 关键修复：移除 .WithProperty("PackageVersion", ...)，避免覆盖 csproj 内 <Version>$(PluginVersion)</Version>
    public DotNetMSBuildSettings CreatePluginMSBuildSettings(PluginProjectInfo plugin)
    {
        var settings = BaseSettings()
            .WithProperty("IsPluginProject", "true")
            .WithProperty("PluginId", plugin.PluginId)
            .WithProperty("PluginName", $"\"{plugin.PluginName}\"")
            .WithProperty("PluginAuthor", plugin.PluginAuthor)
            .WithProperty("PluginDescription", $"\"{plugin.PluginDescription}\"");

        if (!string.IsNullOrEmpty(PluginVersionOverride))
            settings.SetVersion(PluginVersionOverride);
        else if (!string.IsNullOrEmpty(PackageVersion))
            settings.SetVersion(PackageVersion);
        // 否则：不设 Version，让 csproj <Version>$(PluginVersion)</Version> 生效
        return settings;
    }

    private DotNetMSBuildSettings BaseSettings()
    {
        return new DotNetMSBuildSettings()
            .SetConfiguration(BuildConfiguration)
            .WithProperty("ContinuousIntegrationBuild", "true");
    }

    // 计算插件最终版本：--plugin-version > --package-version > csproj <PluginVersion>
    // 用于 zip 命名和 plugin.json manifest，确保产物名与运行时版本一致
    public string GetEffectivePluginVersion(PluginProjectInfo plugin)
    {
        if (!string.IsNullOrEmpty(PluginVersionOverride))
            return PluginVersionOverride;
        if (!string.IsNullOrEmpty(PackageVersion))
            return PackageVersion;
        return plugin.PluginVersion;
    }

    public BuildContext(ICakeContext context)
    {
        Target = ParseBuildTarget(context.Argument("build", "all"));
        BuildConfiguration = context.Argument("configuration", "Release");
        HostVersionOverride = context.Argument("host-version", "");
        PluginVersionOverride = context.Argument("plugin-version", "");
        // 默认空：不覆盖，让 csproj 真相源生效；传值则全覆盖（兼容旧用法）
        PackageVersion = context.Argument("package-version", "");
        PluginFilter = context.Argument("plugin", "");
        NuGetSource = context.Argument("nuget-source", "https://api.nuget.org/v3/index.json");
        NuGetApiKey = context.Argument("nuget-api-key", "");
        RuntimeIdentifier = context.Argument("runtime-identifier", "");
        SelfContained = context.Argument("self-contained", false);

        RootDir = context.Environment.WorkingDirectory.FullPath;
        PackagesDir = Path.Combine(RootDir, "bin");
        NuGetPackagesDir = Path.Combine(PackagesDir, "nuget");
        BinPackagesDir = Path.Combine(PackagesDir, "bin");
        PluginPackagesDir = Path.Combine(PackagesDir, "plugins");

        GeneratorsProject = Path.Combine(RootDir, "src", "Avalonia.Plugin.Generators", "Avalonia.Plugin.Generators.csproj");
        SharedProject = Path.Combine(RootDir, "src", "Avalonia.Plugin.Shared", "Avalonia.Plugin.Shared.csproj");
        LauncherProject = Path.Combine(RootDir, "src", "launcher", "Avalonia.Launcher.Desktop", "Avalonia.Launcher.Desktop.csproj");

        PluginProjects = FilterPlugins(DiscoverPlugins(RootDir), PluginFilter);
    }

    private static IReadOnlyList<PluginProjectInfo> FilterPlugins(IReadOnlyList<PluginProjectInfo> all, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return all;

        var names = new HashSet<string>(
            filter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);

        var matched = all.Where(p => names.Contains(p.ProjectName)).ToList();
        if (matched.Count == 0)
            throw new InvalidOperationException(
                $"--plugin 过滤无匹配项 '{filter}'。可用插件：{string.Join(", ", all.Select(p => p.ProjectName))}");

        return matched;
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
            var minPluginSdkVersion = doc.Descendants("MinPluginSdkVersion").FirstOrDefault()?.Value ?? "0.0.0";

            plugins.Add(new PluginProjectInfo(
                projectName, pluginId, pluginName, pluginVersion, pluginAuthor, pluginDescription, minPluginSdkVersion));
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
                // 兼容：nuget 已与 bin 合并，等价映射
                "nuget" => BuildTarget.Bin,
                "plugin" => BuildTarget.Plugin,
                _ => throw new ArgumentException($"Unknown build target: '{part}'. Valid values: all, bin, plugin")
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
    string PluginDescription,
    string MinPluginSdkVersion)
{
    public string ProjectPath(string rootDir) => Path.Combine(rootDir, "plugins", ProjectName, $"{ProjectName}.csproj");
}
