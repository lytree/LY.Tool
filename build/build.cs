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

        CleanDirectoryIfExists(c, Path.Combine(buildContext.RootDir, "src", "LYBox.Plugin.Generators", "bin"));
        CleanDirectoryIfExists(c, Path.Combine(buildContext.RootDir, "src", "LYBox.Plugin.Generators", "obj"));
        CleanDirectoryIfExists(c, Path.Combine(buildContext.RootDir, "src", "LYBox.Plugin.Shared", "bin"));
        CleanDirectoryIfExists(c, Path.Combine(buildContext.RootDir, "src", "LYBox.Plugin.Shared", "obj"));

        CleanDirectoryIfExists(c, Path.Combine(buildContext.RootDir, "src", "launcher", "LYBox.Launcher.Desktop", "bin"));
        CleanDirectoryIfExists(c, Path.Combine(buildContext.RootDir, "src", "launcher", "LYBox.Launcher.Desktop", "obj"));
    }

    if (t.HasFlag(BuildTarget.FluentWindow))
    {
        CleanDirectoryIfExists(c, Path.Combine(buildContext.RootDir, "src", "LYBox.Layout.Fluent", "bin"));
        CleanDirectoryIfExists(c, Path.Combine(buildContext.RootDir, "src", "LYBox.Layout.Fluent", "obj"));
    }

    if (t.HasFlag(BuildTarget.Tool))
    {
        CleanDirectoryIfExists(c, buildContext.ToolPackagesDir);
        CleanDirectoryIfExists(c, Path.Combine(buildContext.RootDir, "tools", "LYBox.MockServer", "bin"));
        CleanDirectoryIfExists(c, Path.Combine(buildContext.RootDir, "tools", "LYBox.MockServer", "obj"));
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

    // FluentWindow 独立布局项目构建
    if (buildContext.Target.HasFlag(BuildTarget.FluentWindow))
    {
        c.DotNetBuild(buildContext.FluentWindowProject, new DotNetBuildSettings
        {
            Configuration = buildContext.BuildConfiguration,
            MSBuildSettings = hostSettings
        });
        c.Log.Information("FluentWindow project built.");
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
    // 注：插件 restore 依赖 bin/nuget/ 本地 feed，必须等上面的 SDK pack 完成
    if (buildContext.Target.HasFlag(BuildTarget.Plugin))
    {
        var buildFailedPlugins = new List<string>();
        foreach (var plugin in buildContext.PluginProjects)
        {
            var pluginMsBuild = buildContext.CreatePluginMSBuildSettings(plugin);

            try
            {
                c.DotNetBuild(plugin.ProjectPath(buildContext.RootDir), new DotNetBuildSettings
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
        {
            c.Log.Warning("以下 {0} 个插件编译失败: {1}", buildFailedPlugins.Count, string.Join(", ", buildFailedPlugins));
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

    var failedPlugins = new List<string>();
    foreach (var plugin in buildContext.PluginProjects)
    {
        var pluginOutputDir = Path.Combine(buildContext.PluginPackagesDir, plugin.ProjectName, "publish");
        c.EnsureDirectoryExists(pluginOutputDir);

        var pluginMsBuild = buildContext.CreatePluginMSBuildSettings(plugin);

        try
        {
            c.DotNetPublish(plugin.ProjectPath(buildContext.RootDir), new DotNetPublishSettings
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
    {
        c.Log.Warning("以下 {0} 个插件发布失败: {1}", failedPlugins.Count, string.Join(", ", failedPlugins));
    }

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

Task("PackFluentWindow")
    .IsDependentOn("Build")
    .WithCriteria(c => buildContext.Target.HasFlag(BuildTarget.FluentWindow), "FluentWindow target not selected")
    .Does(c =>
{
    var fwOutputDir = Path.Combine(buildContext.PackagesDir, "fluent-window");
    c.EnsureDirectoryExists(fwOutputDir);

    var settings = new DotNetPublishSettings
    {
        Configuration = buildContext.BuildConfiguration,
        OutputDirectory = fwOutputDir,
        NoRestore = true,
        NoBuild = true,
    };

    if (!string.IsNullOrEmpty(buildContext.RuntimeIdentifier))
    {
        settings.Runtime = buildContext.RuntimeIdentifier;
        settings.OutputDirectory = Path.Combine(fwOutputDir, buildContext.RuntimeIdentifier);
        settings.NoBuild = false;
        settings.NoRestore = false;
    }

    if (buildContext.SelfContained)
    {
        settings.SelfContained = true;
    }

    c.DotNetPublish(buildContext.FluentWindowProject, settings);

    c.Log.Information("FluentWindow published to: {0}", fwOutputDir);
});

Task("PackTool")
    .IsDependentOn("Build")
    .WithCriteria(c => buildContext.Target.HasFlag(BuildTarget.Tool), "Tool target not selected")
    .Does(c =>
{
    if (!File.Exists(buildContext.ToolProject))
    {
        c.Log.Warning("Tool project not found at {0}, skipping PackTool", buildContext.ToolProject);
        return;
    }

    c.EnsureDirectoryExists(buildContext.ToolPackagesDir);

    var hostSettings = buildContext.CreateHostMSBuildSettings();

    // PackAsTool 生成可安装的 nupkg（NoBuild=true 复用 Build 任务结果）
    c.DotNetPack(buildContext.ToolProject, new DotNetPackSettings
    {
        Configuration = buildContext.BuildConfiguration,
        OutputDirectory = buildContext.ToolPackagesDir,
        NoRestore = true,
        NoBuild = true,
        MSBuildSettings = hostSettings
    });

    foreach (var pkg in c.GetFiles(Path.Combine(buildContext.ToolPackagesDir, "*.nupkg")))
    {
        c.Log.Information("  Tool NuGet: {0}", pkg.GetFilename());
    }

    c.Log.Information("LYBox.MockServer dotnet tool packed to: {0}", buildContext.ToolPackagesDir);
    c.Log.Information("Install with: dotnet tool install --global --add-source {0} LYBox.MockServer", buildContext.ToolPackagesDir);
    c.Log.Information("Then run: lybox-mock --help");
});

Task("Default")
    .IsDependentOn("PackBin")
    .IsDependentOn("PackFluentWindow")
    .IsDependentOn("PackPlugins")
    .IsDependentOn("PackTool");

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
    // FluentWindow 独立布局项目（自定义边框窗口）
    FluentWindow = 2,
    Plugin = 4,
    // Tool 独立 dotnet tool 项目（lybox-mock 前端调试 Mock 后端）
    Tool = 8,
    All = Bin | FluentWindow | Plugin | Tool
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
    //
    // 版本解析优先级（高 → 低）：
    //   1. --host-version 命令行参数              （显式覆盖，CI workflow_dispatch 使用）
    //   2. --package-version 命令行参数            （兼容旧用法，覆盖所有层）
    //   3. GitVersion.Tool 计算结果                （由 build.cs 自动调用 dotnet gitversion 获取）
    //   4. csproj 真相源 HostVersion               （Directory.Build.props Fallback）
    public string? HostVersionOverride { get; }
    public string? PluginVersionOverride { get; }

    // 兼容回退：显式传 --package-version 时覆盖所有层（紧急发版用）
    public string? PackageVersion { get; }

    // GitVersion 解析结果（null 表示未运行或失败）
    // 用于日志输出与调试，实际生效通过 HostVersionOverride 传入
    public string? GitVersionResolved { get; }

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
    public string ToolPackagesDir { get; }

    public string GeneratorsProject { get; }
    public string SharedProject { get; }
    public string LauncherProject { get; }
    public string FluentWindowProject { get; }
    public string ToolProject { get; }
    public IReadOnlyList<PluginProjectInfo> PluginProjects { get; }

    // 宿主+SDK 版本覆盖（优先级：--host-version > --package-version > GitVersion > csproj 真相源 HostVersion）
    // 宿主与 SDK 共用 HostVersion，一份 settings 即可
    // 显式覆盖（--host-version / --package-version）优先于自动计算（GitVersion），保持向后兼容
    public DotNetMSBuildSettings CreateHostMSBuildSettings()
    {
        var settings = BaseSettings();
        var effective = EffectiveHostVersion;
        if (!string.IsNullOrEmpty(effective))
            settings.SetVersion(effective);
        return settings;
    }

    // 计算实际生效的宿主版本：--host-version > --package-version > GitVersion
    // 返回 null 时由 csproj 内 HostVersion Fallback 生效
    public string? EffectiveHostVersion
    {
        get
        {
            if (!string.IsNullOrEmpty(HostVersionOverride))
                return HostVersionOverride;
            if (!string.IsNullOrEmpty(PackageVersion))
                return PackageVersion;
            if (!string.IsNullOrEmpty(GitVersionResolved))
                return GitVersionResolved;
            return null;
        }
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
        ToolPackagesDir = Path.Combine(PackagesDir, "tools");

        GeneratorsProject = Path.Combine(RootDir, "src", "LYBox.Plugin.Generators", "LYBox.Plugin.Generators.csproj");
        SharedProject = Path.Combine(RootDir, "src", "LYBox.Plugin.Shared", "LYBox.Plugin.Shared.csproj");
        LauncherProject = Path.Combine(RootDir, "src", "launcher", "LYBox.Launcher.Desktop", "LYBox.Launcher.Desktop.csproj");
        FluentWindowProject = Path.Combine(RootDir, "src", "LYBox.Layout.Fluent", "LYBox.Layout.Fluent.csproj");
        ToolProject = Path.Combine(RootDir, "tools", "LYBox.MockServer", "LYBox.MockServer.csproj");

        PluginProjects = FilterPlugins(DiscoverPlugins(RootDir), PluginFilter);

        // GitVersion 解析：仅在未显式指定 --host-version / --package-version 时尝试
        // 优先级：--host-version > --package-version > GitVersion > csproj Fallback
        if (string.IsNullOrEmpty(HostVersionOverride) && string.IsNullOrEmpty(PackageVersion))
        {
            var gitVer = TryResolveGitVersion(context, RootDir);
            if (!string.IsNullOrEmpty(gitVer))
            {
                GitVersionResolved = gitVer;
                context.Log.Information("GitVersion 解析版本: {0}", gitVer);
            }
            else
            {
                context.Log.Warning("GitVersion 未运行或失败，且未指定 --host-version / --package-version，将使用 csproj Fallback（{0}/Directory.Build.props 中的 LyboxLastReleasedVersion）", RootDir);
            }
        }
        else if (!string.IsNullOrEmpty(HostVersionOverride))
        {
            context.Log.Information("使用显式 --host-version: {0}（跳过 GitVersion）", HostVersionOverride);
        }
        else
        {
            context.Log.Information("使用显式 --package-version: {0}（跳过 GitVersion）", PackageVersion);
        }
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
                "fluent-window" => BuildTarget.FluentWindow,
                "fluentwindow" => BuildTarget.FluentWindow,
                // 兼容：nuget 已与 bin 合并，等价映射
                "nuget" => BuildTarget.Bin,
                "plugin" => BuildTarget.Plugin,
                "tool" => BuildTarget.Tool,
                _ => throw new ArgumentException($"Unknown build target: '{part}'. Valid values: all, bin, fluent-window, plugin, tool")
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
