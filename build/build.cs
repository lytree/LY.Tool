#!/usr/bin/env dotnet
#:sdk Cake.Sdk@6.2.0
#:package Spectre.Console@0.57.2
#:property PublishAot=false

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
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
using Spectre.Console;
// Disambiguate System.IO types from Cake.Core.IO.Path / Cake.Common helpers
using Path = System.IO.Path;
using File = System.IO.File;
using Directory = System.IO.Directory;
using Architecture = System.Runtime.InteropServices.Architecture;

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
        CleanDirectoryIfExists(c, buildContext.LauncherPublishDir);
        CleanDirectoryIfExists(c, buildContext.LegacyPackageDir);

        CleanDirectoryIfExists(c, buildContext.DesktopPublishDir);
        CleanDirectoryIfExists(c, buildContext.ConsolePublishDir);
    }

    if (t.HasFlag(BuildTarget.Tool))
    {
        CleanDirectoryIfExists(c, buildContext.ToolPackagesDir);
    }

    if (t.HasFlag(BuildTarget.Plugin))
    {
        CleanDirectoryIfExists(c, buildContext.PluginPackagesDir);
        CleanDirectoryIfExists(c, buildContext.PluginZipPackagesDir);

        foreach (var plugin in buildContext.PluginProjects)
        {
            CleanDirectoryIfExists(c, Path.Combine(buildContext.PluginPackagesDir, plugin.ProjectName));
        }
    }

    if (t.HasFlag(BuildTarget.All))
    {
        CleanDirectoryIfExists(c, buildContext.ArtifactsDir);
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

    // 关键：NuGet pack 必须在插件 build 之前完成，因为插件 restore 依赖 artifacts/packages/sdk 本地 feed
    // SDK 编译：Bin 与 NuGet 目标都需要（NuGet 目标下 PackNuGet 以 NoBuild=true 复用此构建结果）
    var needSdk = buildContext.Target.HasFlag(BuildTarget.Bin) || buildContext.Target.HasFlag(BuildTarget.NuGet);
    if (needSdk)
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
    }

    if (buildContext.Target.HasFlag(BuildTarget.Bin))
    {
        // SDK NuGet 打包（NoBuild=true 复用上一步构建结果，输出到 artifacts/packages/sdk）
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
        c.DotNetBuild(buildContext.ConsoleProject, new DotNetBuildSettings
        {
            Configuration = buildContext.BuildConfiguration,
            MSBuildSettings = hostSettings
        });
    }

    // Tool 独立 dotnet tool 项目构建（lybox-mock 前端调试 Mock 后端）
    if (buildContext.Target.HasFlag(BuildTarget.Tool))
    {
        if (File.Exists(buildContext.ToolProject))
        {
            c.DotNetBuild(buildContext.ToolProject, new DotNetBuildSettings
            {
                Configuration = buildContext.BuildConfiguration,
                MSBuildSettings = hostSettings
            });
            c.Log.Information("Tool project built.");
        }
        else
        {
            c.Log.Warning("Tool project not found at {0}, skipping", buildContext.ToolProject);
        }
    }

    // 插件层：各插件用自己的 PluginVersion（不再被 PackageVersion 覆盖）
    // 注：插件 restore 依赖 artifacts/packages/sdk 本地 feed，必须等上面的 SDK pack 完成
    if (buildContext.Target.HasFlag(BuildTarget.Plugin))
    {
        var buildFailedPlugins = new List<string>();
        foreach (var plugin in buildContext.PluginProjects)
        {
            var pluginMsBuild = buildContext.CreatePluginMSBuildSettings(plugin);

            try
            {
                c.DotNetBuild(plugin.ProjectPath, new DotNetBuildSettings
                {
                    Configuration = buildContext.BuildConfiguration,
                    MSBuildSettings = pluginMsBuild
                });
            }
            catch (Exception ex)
            {
                c.Log.Error("插件 {0} 编译失败，跳过（不影响其他插件）: {1}", plugin.ProjectName, ex.Message);
                buildFailedPlugins.Add(plugin.ProjectName);
            }
        }
        if (buildFailedPlugins.Count > 0)
            throw new InvalidOperationException($"以下 {buildFailedPlugins.Count} 个插件编译失败: {string.Join(", ", buildFailedPlugins)}");
    }

    c.Log.Information("Build completed. Target: {0}", buildContext.Target);
});

Task("PackNuGet")
    .IsDependentOn("Build")
    .WithCriteria(c => buildContext.Target.HasFlag(BuildTarget.NuGet))
    .Does(c => BuildTasks.PackNuGet(buildContext));

Task("PackBin")
    .IsDependentOn("Build")
    .WithCriteria(c => buildContext.Target.HasFlag(BuildTarget.Bin))
    .Does(c =>
{
    // 发布宿主 launcher（GUI 版）
    c.EnsureDirectoryExists(buildContext.DesktopPublishDir);

    var settings = new DotNetPublishSettings
    {
        Configuration = buildContext.BuildConfiguration,
        OutputDirectory = buildContext.DesktopPublishDir,
        NoRestore = true,
        NoBuild = true,
    };

    if (!string.IsNullOrEmpty(buildContext.RuntimeIdentifier))
    {
        settings.Runtime = buildContext.RuntimeIdentifier;
        settings.OutputDirectory = Path.Combine(buildContext.DesktopPublishDir, buildContext.RuntimeIdentifier);
        // Build 未按 RID 编译，publish 需要重新构建 RID 产物
        settings.NoBuild = false;
        settings.NoRestore = false;
    }

    if (buildContext.SelfContained)
    {
        settings.SelfContained = true;
    }

    c.DotNetPublish(buildContext.LauncherProject, settings);

    // 同时发布控制台调试版（LYBox.Launcher.Console.exe），两个可执行文件共用同一套启动逻辑
    var consoleSettings = new DotNetPublishSettings
    {
        Configuration = buildContext.BuildConfiguration,
        OutputDirectory = buildContext.ConsolePublishDir,
        NoRestore = true,
        NoBuild = true,
    };

    if (!string.IsNullOrEmpty(buildContext.RuntimeIdentifier))
    {
        consoleSettings.Runtime = buildContext.RuntimeIdentifier;
        consoleSettings.OutputDirectory = Path.Combine(buildContext.ConsolePublishDir, buildContext.RuntimeIdentifier);
        consoleSettings.NoBuild = false;
        consoleSettings.NoRestore = false;
    }

    if (buildContext.SelfContained)
    {
        consoleSettings.SelfContained = true;
    }

    c.DotNetPublish(buildContext.ConsoleProject, consoleSettings);

    c.Log.Information("Desktop launcher published to: {0}", settings.OutputDirectory);
    c.Log.Information("Console launcher published to: {0}", consoleSettings.OutputDirectory);
});

Task("LocalInstall")
    .IsDependentOn("PackNuGet")
    .Does(c => BuildTasks.LocalInstall(buildContext));

Task("PublishNuGet")
    .IsDependentOn("PackNuGet")
    .WithCriteria(c => buildContext.Target.HasFlag(BuildTarget.NuGetPublish))
    .Does(c => BuildTasks.PublishNuGet(buildContext));

Task("PackPlugins")
    .IsDependentOn("Build")
    .WithCriteria(c => buildContext.Target.HasFlag(BuildTarget.Plugin), "Plugin target not selected")
    .Does(c =>
{
    c.EnsureDirectoryExists(buildContext.PluginPackagesDir);

    var failedPlugins = new List<string>();
    foreach (var plugin in buildContext.PluginProjects)
    {
        var pluginOutputDir = Path.Combine(buildContext.PluginPackagesDir, plugin.ProjectName, "publish");
        c.EnsureDirectoryExists(pluginOutputDir);

        var pluginMsBuild = buildContext.CreatePluginMSBuildSettings(plugin);

        try
        {
            c.DotNetPublish(plugin.ProjectPath, new DotNetPublishSettings
            {
                Configuration = buildContext.BuildConfiguration,
                OutputDirectory = pluginOutputDir,
                MSBuildSettings = pluginMsBuild
            });

            // 复制插件 wwwroot/ 前端资源到发布目录（仅当源目录存在时）
            CopyPluginWwwroot(c, buildContext, plugin, pluginOutputDir);

            c.Log.Information("Plugin published: {0} -> {1}", plugin.ProjectName, pluginOutputDir);
        }
        catch (Exception ex)
        {
            c.Log.Error("插件 {0} 发布失败，跳过（不影响其他插件）: {1}", plugin.ProjectName, ex.Message);
            failedPlugins.Add(plugin.ProjectName);
        }
    }

    PackPluginZips(c, buildContext);

    if (failedPlugins.Count > 0)
        throw new InvalidOperationException($"以下 {failedPlugins.Count} 个插件发布失败: {string.Join(", ", failedPlugins)}");

    c.Log.Information("All plugins published to: {0}", buildContext.PluginPackagesDir);

    static void CopyPluginWwwroot(ICakeContext ctx, BuildContext bctx, PluginProjectInfo plugin, string publishDir)
    {
        var pluginSrcDir = Path.Combine(bctx.RootDir, "plugins", plugin.ProjectName);
        var wwwrootSrc = Path.Combine(pluginSrcDir, "wwwroot");

        if (!Directory.Exists(wwwrootSrc))
        {
            ctx.Log.Debug("插件 {0} 无 wwwroot 目录，跳过前端资源复制", plugin.ProjectName);
            return;
        }

        var wwwrootDest = Path.Combine(publishDir, "wwwroot");
        CopyDirectoryRecursive(wwwrootSrc, wwwrootDest);
        ctx.Log.Information("插件 {0} wwwroot 已复制到 {1}", plugin.ProjectName, wwwrootDest);
    }

    static void CopyDirectoryRecursive(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.TopDirectoryOnly))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
        }
        foreach (var subDir in Directory.GetDirectories(sourceDir, "*", SearchOption.TopDirectoryOnly))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
            CopyDirectoryRecursive(subDir, destSubDir);
        }
    }

    static void PackPluginZips(ICakeContext ctx, BuildContext bctx)
    {
        var zipOutputDir = bctx.PluginZipPackagesDir;
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
                    if (extension.Equals(".pdb", StringComparison.OrdinalIgnoreCase) ||
                        extension.Equals(".xml", StringComparison.OrdinalIgnoreCase))
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

Task("PackTool")
    .IsDependentOn("Build")
    .WithCriteria(c => buildContext.Target.HasFlag(BuildTarget.Tool))
    .Does(c => BuildTasks.PackTool(buildContext));

Task("Default")
    .IsDependentOn("PackBin")
    .IsDependentOn("PackNuGet")
    .IsDependentOn("PackPlugins")
    .IsDependentOn("PackTool")
    .IsDependentOn("PublishNuGet");

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
    // Bin：构建宿主 launcher + console（并同时产出 SDK NuGet 包，供插件 restore）
    Bin = 1,
    // NuGet：仅打包 SDK NuGet 包（Generators + Shared）到 bin/nuget/
    NuGet = 2,
    // Plugin：构建并打包插件为 zip
    Plugin = 4,
    // NuGetPublish：推送 bin/nuget/ 下的 SDK 包到 NuGet 源
    NuGetPublish = 8,
    // Tool：打包 LYBox.MockServer dotnet tool（lybox-mock）
    Tool = 16,
    All = Bin | NuGet | Plugin | Tool
}

/// <summary>
/// 包装 ICakeContext，集中管理构建参数、目录解析、版本覆盖与交互式提示。
/// </summary>
public class BuildContext
{
    private ICakeContext Cake { get; }

    public BuildTarget Target { get; }
    public string BuildConfiguration { get; }

    // 三层独立版本覆盖：宿主 / SDK / 插件 各自管理，留空时由各层 csproj 真相源决定
    //
    // 版本解析优先级（高 → 低）：
    //   1. --host-version   （宿主 launcher 显式覆盖，CI workflow_dispatch 使用）
    //   2. --sdk-version    （SDK Generators + Shared 显式覆盖）
    //   3. --plugin-version （每个插件显式覆盖）
    //   4. --package-version（兼容旧用法，作为未单独指定层的全局兜底）
    //   5. 各层 csproj 真相源（HostVersion / <PluginVersion> / <Version> Fallback）
    public string? HostVersionOverride { get; }
    public string? SdkVersionOverride { get; }
    public string? PluginVersionOverride { get; }

    // 兼容回退：显式传 --package-version 时作为未指定层的全局兜底（紧急发版用）
    public string? PackageVersion { get; }

    // 插件过滤：--plugin=<Name> 只构建匹配的插件（逗号分隔多个）
    public string? PluginFilter { get; }

    public string NuGetSource { get; }
    public string NuGetApiKey { get; }
    public string RuntimeIdentifier { get; }
    public bool SelfContained { get; }
    public bool NoBuild { get; }

    public string RootDir { get; }
    public string ArtifactsDir { get; }
    public string NuGetPackagesDir { get; }
    public string LauncherPublishDir { get; }
    public string PluginPackagesDir { get; }
    public string PluginZipPackagesDir { get; }
    public string ToolPackagesDir { get; }
    public string DesktopPublishDir { get; }
    public string ConsolePublishDir { get; }
    public string LegacyPackageDir { get; }

    public string GeneratorsProject { get; }
    public string SharedProject { get; }
    public string LauncherProject { get; }
    public string ConsoleProject { get; }
    public string ToolProject { get; }
    public IReadOnlyList<PluginProjectInfo> PluginProjects { get; }

    // 宿主版本覆盖（优先级：--host-version > --package-version > csproj 真相源 HostVersion）
    public DotNetMSBuildSettings CreateHostMSBuildSettings()
    {
        var settings = BaseSettings();
        var effective = EffectiveHostVersion;
        if (!string.IsNullOrEmpty(effective))
            settings.SetVersion(effective);
        return settings;
    }

    // 计算实际生效的宿主版本：--host-version > --package-version
    // 返回 null 时由 csproj 内 HostVersion Fallback 生效
    public string? EffectiveHostVersion
    {
        get
        {
            if (!string.IsNullOrEmpty(HostVersionOverride))
                return HostVersionOverride;
            if (!string.IsNullOrEmpty(PackageVersion))
                return PackageVersion;
            return null;
        }
    }

    // 计算实际生效的 SDK 版本：--sdk-version > --package-version
    // 返回 null 时由 SDK csproj 内 <Version> Fallback 生效
    public string? EffectiveSdkVersion
    {
        get
        {
            if (!string.IsNullOrEmpty(SdkVersionOverride))
                return SdkVersionOverride;
            if (!string.IsNullOrEmpty(PackageVersion))
                return PackageVersion;
            return null;
        }
    }

    // SDK（Generators + Shared）独立版本，与宿主版本解耦
    public DotNetMSBuildSettings CreateSdkMSBuildSettings()
    {
        var settings = BaseSettings();
        var effective = EffectiveSdkVersion;
        if (!string.IsNullOrEmpty(effective))
            settings.SetVersion(effective);
        return settings;
    }

    // 插件版本覆盖（优先级：--plugin-version > --package-version > csproj <PluginVersion>）
    // 关键修复：不设置 PackageVersion，避免覆盖 csproj 内 <Version>$(PluginVersion)</Version>
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
        Cake = context;

        var requestedBuildTarget = context.Argument("build", "");
        Target = SelectBuildTarget(
            ParseBuildTarget(requestedBuildTarget),
            !string.IsNullOrWhiteSpace(requestedBuildTarget));
        var requestedBuildConfiguration = context.Argument("configuration", "");
        BuildConfiguration = SelectBuildConfiguration(
            requestedBuildConfiguration,
            !string.IsNullOrWhiteSpace(requestedBuildConfiguration));

        HostVersionOverride = context.Argument("host-version", "");
        SdkVersionOverride = context.Argument("sdk-version", "");
        PluginVersionOverride = context.Argument("plugin-version", "");
        // 默认空：不覆盖，让各层 csproj 真相源各自生效；传值则作为未指定层的全局兜底（兼容旧用法）
        PackageVersion = context.Argument("package-version", "");
        PluginFilter = context.Argument("plugin", "");
        NuGetSource = context.Argument("nuget-source", "https://api.nuget.org/v3/index.json");
        NuGetApiKey = context.Argument("nuget-api-key", "");
        RuntimeIdentifier = SelectRuntimeIdentifier(Target, NormalizeRuntimeIdentifier(context.Argument("runtime-identifier", "")));
        SelfContained = SelectSelfContained(Target, context.Argument("self-contained", false), context.HasArgument("self-contained"));
        NoBuild = context.Argument("no-build", false);

        RootDir = ResolveRepositoryRoot();
        ArtifactsDir = Path.Combine(RootDir, "artifacts");
        NuGetPackagesDir = Path.Combine(ArtifactsDir, "packages", "sdk");
        LauncherPublishDir = Path.Combine(ArtifactsDir, "publish", "launcher");
        PluginPackagesDir = Path.Combine(ArtifactsDir, "publish", "plugins");
        PluginZipPackagesDir = Path.Combine(ArtifactsDir, "packages", "plugins");
        ToolPackagesDir = Path.Combine(ArtifactsDir, "packages", "tools");
        DesktopPublishDir = Path.Combine(LauncherPublishDir, "desktop");
        ConsolePublishDir = Path.Combine(LauncherPublishDir, "console");
        LegacyPackageDir = Path.Combine(ArtifactsDir, "package");

        GeneratorsProject = Path.Combine(RootDir, "src", "Plugin", "LYBox.Plugin.Generators", "LYBox.Plugin.Generators.csproj");
        SharedProject = Path.Combine(RootDir, "src", "Plugin", "LYBox.Plugin.Shared", "LYBox.Plugin.Shared.csproj");
        LauncherProject = Path.Combine(RootDir, "src", "App", "LYBox.Launcher.Desktop", "LYBox.Launcher.Desktop.csproj");
        ConsoleProject = Path.Combine(RootDir, "src", "App", "LYBox.Launcher.Console", "LYBox.Launcher.Console.csproj");
        ToolProject = Path.Combine(RootDir, "tools", "LYBox.MockServer", "LYBox.MockServer.csproj");

        var discoveredPlugins = DiscoverPlugins(RootDir);
        PluginProjects = SelectPluginFilters(Target, PluginFilter, discoveredPlugins);
    }

    public ICakeLog Log => Cake.Log;
    public void EnsureDirectoryExists(string path) => Cake.EnsureDirectoryExists(path);
    public void CleanDirectory(string path) => Cake.CleanDirectory(path);
    public void DotNetBuild(string project, DotNetBuildSettings settings) => Cake.DotNetBuild(project, settings);
    public void DotNetPack(string project, DotNetPackSettings settings) => Cake.DotNetPack(project, settings);
    public void DotNetPublish(string project, DotNetPublishSettings settings) => Cake.DotNetPublish(project, settings);
    public IEnumerable<Cake.Core.IO.FilePath> GetFiles(string pattern) => Cake.GetFiles(pattern);
    public int StartProcess(string fileName, Cake.Core.IO.ProcessSettings settings) => Cake.StartProcess(fileName, settings);

    private static string ResolveRepositoryRoot([CallerFilePath] string sourceFilePath = "")
    {
        var buildDirectory = Path.GetDirectoryName(sourceFilePath)
            ?? throw new InvalidOperationException("无法解析 build.cs 所在目录");
        return Path.GetFullPath(Path.Combine(buildDirectory, ".."));
    }

    /// <summary>
    /// 调用 dotnet gitversion /showvariable FullSemVer 获取版本号。
    /// 失败时返回 null（不抛异常），由调用方决定是否降级处理。
    /// 前置条件：已运行 `dotnet tool restore`（安装 GitVersion.Tool 到 .config/dotnet-tools.json）。
    /// </summary>
    private static string? TryResolveGitVersion(ICakeContext context, string rootDir)
    {
        try
        {
            // 使用 System.Diagnostics.Process 直接调用，避免 Cake StartProcess 输出重载 API 差异
            // 通过 `dotnet gitversion` 调用，自动定位 .config/dotnet-tools.json
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "gitversion /showvariable FullSemVer /nocache",
                WorkingDirectory = rootDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc == null)
            {
                context.Log.Debug("dotnet gitversion 进程启动失败（Process.Start 返回 null）");
                return null;
            }

            var stdout = proc.StandardOutput.ReadToEnd();
            var stderr = proc.StandardError.ReadToEnd();
            var exited = proc.WaitForExit(TimeSpan.FromSeconds(30)); // 30s 超时

            if (!exited || !proc.HasExited)
            {
                try { proc.Kill(); } catch { /* ignore */ }
                context.Log.Debug("dotnet gitversion 超时未退出（30s），已终止");
                return null;
            }

            if (proc.ExitCode != 0)
            {
                context.Log.Debug("dotnet gitversion 退出码 {0}。stderr: {1}", proc.ExitCode, stderr.Trim());
                return null;
            }

            var version = stdout.Trim();
            if (string.IsNullOrWhiteSpace(version))
            {
                context.Log.Debug("dotnet gitversion 输出为空");
                return null;
            }

            // 简单校验：至少包含数字
            if (!version.Any(char.IsDigit))
            {
                context.Log.Debug("dotnet gitversion 输出不像版本号: {0}", version);
                return null;
            }

            return version;
        }
        catch (Exception ex)
        {
            context.Log.Debug("dotnet gitversion 调用异常: {0}", ex.Message);
            return null;
        }
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
                Path.GetFullPath(csprojFile), projectName, pluginId, pluginName, pluginVersion, pluginAuthor, pluginDescription, minPluginSdkVersion));
        }

        return plugins.OrderBy(plugin => plugin.ProjectPath, StringComparer.OrdinalIgnoreCase).ToArray();
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
                "tool" => BuildTarget.Tool,
                "publish-nuget" or "nuget-publish" or "push-nuget" => BuildTarget.NuGetPublish,
                _ => throw new ArgumentException($"Unknown build target: '{part}'. Valid values: all, bin, nuget, plugin, tool, publish-nuget")
            };
        }
        return result == BuildTarget.None ? BuildTarget.All : result;
    }

    // ---- 交互式提示（未传参且终端可交互时触发）----

    private static bool CanPrompt => !Console.IsInputRedirected && !Console.IsOutputRedirected;

    private static BuildTarget SelectBuildTarget(BuildTarget requestedTarget, bool isConfigured)
    {
        if (isConfigured || !CanPrompt)
            return requestedTarget;

        AnsiConsole.Write(new Rule("[yellow]未指定 --build：请选择构建目标[/]")
            .RuleStyle("grey"));

        var mode = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("选择构建方式")
                .AddChoices("构建全部", "选择构建目标"));

        if (mode == "构建全部")
            return BuildTarget.All;

        var selectedTargets = AnsiConsole.Prompt(
            new MultiSelectionPrompt<BuildTarget>()
                .Title("选择构建目标")
                .InstructionsText("[grey]使用 [blue]↑[/]/[blue]↓[/] 移动，按 [blue]Space[/] 勾选，按 [blue]Enter[/] 确认。[/]")
                .UseConverter(GetBuildTargetDisplayName)
                .AddChoices(
                    BuildTarget.Bin,
                    BuildTarget.NuGet,
                    BuildTarget.Plugin,
                    BuildTarget.Tool,
                    BuildTarget.NuGetPublish));

        var combined = selectedTargets.Aggregate(BuildTarget.None, (acc, item) => acc | item);
        return combined.HasFlag(BuildTarget.NuGetPublish) ? combined | BuildTarget.NuGet : combined;
    }

    private static string GetBuildTargetDisplayName(BuildTarget target)
    {
        return target switch
        {
            BuildTarget.Bin => "宿主启动器 (bin)",
            BuildTarget.NuGet => "SDK NuGet 包 (nuget)",
            BuildTarget.Plugin => "插件包 (plugin)",
            BuildTarget.Tool => "CLI 工具 (tool)",
            BuildTarget.NuGetPublish => "打包并发布 NuGet (publish-nuget)",
            _ => target.ToString(),
        };
    }

    private static string SelectBuildConfiguration(string requestedConfiguration, bool isConfigured)
    {
        if (isConfigured || !CanPrompt)
            return string.IsNullOrWhiteSpace(requestedConfiguration) ? "Release" : requestedConfiguration;

        AnsiConsole.Write(new Rule("[yellow]未指定 --configuration：请选择构建配置[/]")
            .RuleStyle("grey"));

        return AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("选择构建配置")
                .AddChoices("Release", "Debug"));
    }

    private static bool SelectSelfContained(BuildTarget target, bool requestedSelfContained, bool isConfigured)
    {
        if (!target.HasFlag(BuildTarget.Bin) || isConfigured || !CanPrompt)
            return requestedSelfContained;

        AnsiConsole.Write(new Rule("[yellow]未指定 --self-contained：请选择发布模式[/]")
            .RuleStyle("grey"));

        return AnsiConsole.Prompt(
            new SelectionPrompt<bool>()
                .Title("生成自包含发布包？")
                .UseConverter(value => value ? "是：包含 .NET 运行时" : "否：需要目标机器已安装 .NET 运行时")
                .AddChoices(true, false));
    }

    private static string SelectRuntimeIdentifier(BuildTarget target, string requestedRuntimeIdentifier)
    {
        if (!target.HasFlag(BuildTarget.Bin) || !string.IsNullOrWhiteSpace(requestedRuntimeIdentifier) || !CanPrompt)
            return requestedRuntimeIdentifier;

        AnsiConsole.Write(new Rule("[yellow]未指定 --runtime-identifier：请选择目标运行时[/]")
            .RuleStyle("grey"));

        var choices = CreateRuntimeIdentifierChoices();
        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<RuntimeIdentifierChoice>()
                .Title("选择运行时标识符 (RID)")
                .UseConverter(choice => Markup.Escape(choice.DisplayName))
                .AddChoices(choices));

        return choice.PromptForValue
            ? NormalizeRuntimeIdentifier(PromptForRequiredString("输入自定义运行时标识符 (RID)："))
            : choice.Value;
    }

    private static IReadOnlyList<RuntimeIdentifierChoice> CreateRuntimeIdentifierChoices()
    {
        var choices = new List<RuntimeIdentifierChoice>();
        var current = GetCurrentRuntimeIdentifier();
        if (!string.IsNullOrEmpty(current))
            choices.Add(new RuntimeIdentifierChoice($"当前系统 ({current})", current));

        foreach (var rid in new[] { "win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64" })
        {
            if (!string.Equals(rid, current, StringComparison.OrdinalIgnoreCase))
                choices.Add(new RuntimeIdentifierChoice(rid, rid));
        }

        choices.Add(new RuntimeIdentifierChoice("不指定 RID（使用框架默认发布）", ""));
        choices.Add(new RuntimeIdentifierChoice("输入自定义 RID", "", true));
        return choices;
    }

    private static string PromptForRequiredString(string prompt)
    {
        while (true)
        {
            var value = AnsiConsole.Ask<string>(prompt);
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();

            AnsiConsole.MarkupLine("[red]请输入一个值。[/]");
        }
    }

    private static string GetCurrentRuntimeIdentifier()
    {
        var platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx"
            : "";
        return string.IsNullOrEmpty(platform) ? "" : $"{platform}-{GetDefaultRidArchitecture()}";
    }

    private static string NormalizeRuntimeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var arch = GetDefaultRidArchitecture();
        return value.Trim().ToLowerInvariant() switch
        {
            "win" or "windows" => $"win-{arch}",
            "linux" => $"linux-{arch}",
            "mac" or "macos" or "osx" => $"osx-{arch}",
            _ => value.Trim()
        };
    }

    private static string GetDefaultRidArchitecture()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => throw new ArgumentException(
                $"Unsupported process architecture '{RuntimeInformation.ProcessArchitecture}'. Specify a concrete runtime identifier, for example win-x64 or linux-arm64.")
        };
    }

    private static IReadOnlyList<PluginProjectInfo> SelectPluginFilters(
        BuildTarget target,
        string? requestedFilter,
        IReadOnlyList<PluginProjectInfo> plugins)
    {
        if (!target.HasFlag(BuildTarget.Plugin) || !string.IsNullOrWhiteSpace(requestedFilter) || !CanPrompt)
            return FilterPlugins(plugins, requestedFilter);

        if (plugins.Count == 0)
            return plugins;

        AnsiConsole.Write(new Rule("[yellow]未指定 --plugin：请选择要打包的插件[/]")
            .RuleStyle("grey"));

        var mode = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("选择打包方式")
                .AddChoices("从列表选择", "输入插件名称", "构建全部插件"));

        return mode switch
        {
            "从列表选择" => FilterPlugins(plugins, string.Join(",", SelectPluginsFromList(plugins))),
            "输入插件名称" => FilterPlugins(plugins, PromptForPluginFilters(plugins)),
            "构建全部插件" => plugins,
            _ => throw new InvalidOperationException($"Unsupported plugin selection mode: {mode}"),
        };
    }

    private static IReadOnlyList<string> SelectPluginsFromList(IReadOnlyList<PluginProjectInfo> plugins)
    {
        var selected = AnsiConsole.Prompt(
            new MultiSelectionPrompt<PluginProjectInfo>()
                .Title("选择要打包的插件")
                .InstructionsText("[grey]使用 [blue]↑[/]/[blue]↓[/] 移动，按 [blue]Space[/] 勾选，按 [blue]Enter[/] 确认。[/]")
                .PageSize(Math.Min(plugins.Count, 10))
                .UseConverter(plugin => $"{plugin.ShortName} [grey]({plugin.ProjectName})[/]")
                .AddChoices(plugins));

        return selected.Select(plugin => plugin.ProjectName).ToArray();
    }

    private static string PromptForPluginFilters(IReadOnlyList<PluginProjectInfo> plugins)
    {
        while (true)
        {
            var input = AnsiConsole.Ask<string>("输入插件名称或序号（用逗号分隔；输入 [green]all[/] 构建全部）：");

            var selections = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (selections.Length == 0)
            {
                AnsiConsole.MarkupLine("[red]请输入至少一个插件。[/]");
                continue;
            }

            if (selections.Any(s => string.Equals(s, "all", StringComparison.OrdinalIgnoreCase)))
            {
                if (selections.Length > 1)
                {
                    AnsiConsole.MarkupLine("[red]'all' 不能与其他选项组合。[/]");
                    continue;
                }
                return "";
            }

            var filters = new List<string>();
            var valid = true;
            foreach (var selection in selections)
            {
                if (int.TryParse(selection, out var index))
                {
                    if (index < 1 || index > plugins.Count)
                    {
                        AnsiConsole.MarkupLine($"[red]插件序号 '{selection}' 超出范围。[/]");
                        valid = false;
                        break;
                    }
                    filters.Add(plugins[index - 1].ProjectName);
                }
                else
                {
                    if (!plugins.Any(p => p.Matches(selection)))
                    {
                        AnsiConsole.MarkupLine($"[red]未知插件 '{selection}'。[/]");
                        valid = false;
                        break;
                    }
                    filters.Add(selection);
                }
            }

            if (valid)
                return string.Join(",", filters);
        }
    }

    private sealed record RuntimeIdentifierChoice(string DisplayName, string Value, bool PromptForValue = false);
}

public record PluginProjectInfo(
    string ProjectPath,
    string ProjectName,
    string PluginId,
    string PluginName,
    string PluginVersion,
    string PluginAuthor,
    string PluginDescription,
    string MinPluginSdkVersion)
{
    public string ShortName =>
        ProjectName.StartsWith("LYBox.Plugin.", StringComparison.OrdinalIgnoreCase)
            ? ProjectName["LYBox.Plugin.".Length..]
            : ProjectName;

    public bool Matches(string value)
    {
        var key = NormalizePluginKey(value);
        return key == NormalizePluginKey(ProjectName)
            || key == NormalizePluginKey(ShortName)
            || key == NormalizePluginKey(PluginId)
            || key == NormalizePluginKey(PluginName);
    }

    private static string NormalizePluginKey(string value)
    {
        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }
}

//////////////////////////////////////////////////////////////////////
// BUILD TASKS
//////////////////////////////////////////////////////////////////////

public static class BuildTasks
{
    public static void PackNuGet(BuildContext context)
    {
        context.EnsureDirectoryExists(context.NuGetPackagesDir);

        var sdkSettings = context.CreateSdkMSBuildSettings();

        context.DotNetPack(context.GeneratorsProject, new DotNetPackSettings
        {
            Configuration = context.BuildConfiguration,
            OutputDirectory = context.NuGetPackagesDir,
            NoRestore = true,
            NoBuild = true,
            MSBuildSettings = sdkSettings
        });

        context.DotNetPack(context.SharedProject, new DotNetPackSettings
        {
            Configuration = context.BuildConfiguration,
            OutputDirectory = context.NuGetPackagesDir,
            NoRestore = true,
            NoBuild = true,
            MSBuildSettings = sdkSettings
        });

        context.Log.Information("SDK NuGet packages created in: {0}", context.NuGetPackagesDir);
        foreach (var pkg in context.GetFiles(Path.Combine(context.NuGetPackagesDir, "*.nupkg")))
        {
            context.Log.Information("  NuGet: {0}", pkg.GetFilename());
        }
    }

    public static void LocalInstall(BuildContext context)
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

    public static void PublishNuGet(BuildContext context)
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
            context.StartProcess("dotnet", new Cake.Core.IO.ProcessSettings
            {
                Arguments = new Cake.Core.IO.ProcessArgumentBuilder()
                    .Append("nuget")
                    .Append("push")
                    .AppendQuoted(pkg.FullPath)
                    .Append("--source")
                    .AppendQuoted(context.NuGetSource)
                    .Append("--api-key")
                    .AppendQuoted(context.NuGetApiKey)
                    .Append("--skip-duplicate")
            });
        }

        context.Log.Information("NuGet packages pushed to: {0}", context.NuGetSource);
    }

    public static void PackTool(BuildContext context)
    {
        if (!File.Exists(context.ToolProject))
        {
            context.Log.Warning("Tool project not found at {0}, skipping PackTool", context.ToolProject);
            return;
        }

        context.EnsureDirectoryExists(context.ToolPackagesDir);
        var hostSettings = context.CreateHostMSBuildSettings();

        // PackAsTool 生成可安装的 nupkg（NoBuild=true 复用 Build 任务结果）
        context.DotNetPack(context.ToolProject, new DotNetPackSettings
        {
            Configuration = context.BuildConfiguration,
            OutputDirectory = context.ToolPackagesDir,
            NoRestore = true,
            NoBuild = true,
            MSBuildSettings = hostSettings
        });

        foreach (var pkg in context.GetFiles(Path.Combine(context.ToolPackagesDir, "*.nupkg")))
        {
            context.Log.Information("  Tool NuGet: {0}", pkg.GetFilename());
        }

        context.Log.Information("LYBox.MockServer dotnet tool packed to: {0}", context.ToolPackagesDir);
        context.Log.Information("Install with: dotnet tool install --global --add-source {0} LYBox.MockServer", context.ToolPackagesDir);
        context.Log.Information("Then run: lybox-mock --help");
    }
}
