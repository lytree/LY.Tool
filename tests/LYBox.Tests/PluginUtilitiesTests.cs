using System.Text.Json;
using LYBox.Plugin.Shared.Models;
using LYBox.UrsaWindow.Services;
using Xunit;

namespace LYBox.Tests;

/// <summary>
/// PluginUtilities 共享工具类的单元测试：验证 JsonSerializerOptions 一致性和 CopyDirectory 功能。
/// </summary>
public class PluginUtilitiesTests
{
    #region JsonOptions 一致性

    [Fact]
    public void JsonOptions_IsNotReadOnly()
    {
        // 确保选项实例可被消费方直接使用（非冻结态，但约定不修改）
        Assert.False(PluginUtilities.JsonOptions.IsReadOnly);
    }

    [Fact]
    public void JsonOptions_WriteIndented_IsTrue()
    {
        // 确保统一使用缩进输出（原 PluginLoader 的配置，现在作为标准）
        Assert.True(PluginUtilities.JsonOptions.WriteIndented);
    }

    [Fact]
    public void JsonOptions_PropertyNamingPolicy_IsCamelCase()
    {
        // 确保命名策略为 camelCase
        Assert.Equal(JsonNamingPolicy.CamelCase, PluginUtilities.JsonOptions.PropertyNamingPolicy);
    }

    [Fact]
    public void JsonOptions_SerializeManifest_ProducesCamelCaseJson()
    {
        // 验证序列化 PluginManifest 时正确使用 camelCase
        var manifest = new PluginManifest
        {
            PluginId = "test-id",
            Name = "TestPlugin",
            Version = "1.0.0",
            Author = "TestAuthor",
            Description = "Test description",
            MinPluginSdkVersion = "2.1.0"
        };

        var json = JsonSerializer.Serialize(manifest, PluginUtilities.JsonOptions);

        Assert.Contains("\"pluginId\": \"test-id\"", json);
        Assert.Contains("\"name\": \"TestPlugin\"", json);
        Assert.Contains("\"version\": \"1.0.0\"", json);
        Assert.Contains("\"minPluginSdkVersion\": \"2.1.0\"", json);
    }

    [Fact]
    public void JsonOptions_SerializeAndDeserialize_RoundTrip()
    {
        // 验证序列化和反序列化的往返一致性
        var original = new PluginManifest
        {
            PluginId = "round-trip-id",
            Name = "RoundTripPlugin",
            Version = "3.2.1",
            Author = "RTAuthor",
            Description = "Round trip test",
            MinPluginSdkVersion = "2.0.0"
        };

        var json = JsonSerializer.Serialize(original, PluginUtilities.JsonOptions);
        var deserialized = JsonSerializer.Deserialize<PluginManifest>(json, PluginUtilities.JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(original.PluginId, deserialized.PluginId);
        Assert.Equal(original.Name, deserialized.Name);
        Assert.Equal(original.Version, deserialized.Version);
        Assert.Equal(original.MinPluginSdkVersion, deserialized.MinPluginSdkVersion);
    }

    [Fact]
    public void JsonOptions_SerializePendingUpgradeInfo_ProducesCamelCaseJson()
    {
        // 验证 PendingUpgradeInfo 也使用相同的 camelCase 命名（原 PluginInstallationManager 的用途）
        var info = new PendingUpgradeInfo
        {
            PluginId = "upgrade-target",
            NewVersion = "2.0.0",
            ScheduledAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PreserveState = true
        };

        var json = JsonSerializer.Serialize(info, PluginUtilities.JsonOptions);

        Assert.Contains("\"pluginId\": \"upgrade-target\"", json);
        Assert.Contains("\"newVersion\": \"2.0.0\"", json);
        Assert.Contains("\"preserveState\": true", json);
    }

    #endregion

    #region CopyDirectory

    [Fact]
    public void CopyDirectory_CopiesAllFilesAndSubdirectories()
    {
        // 准备源目录结构
        var tempRoot = Path.Combine(Path.GetTempPath(), $"lybox-test-{Guid.NewGuid():N}");
        var sourceDir = Path.Combine(tempRoot, "source");
        var destDir = Path.Combine(tempRoot, "dest");

        try
        {
            Directory.CreateDirectory(Path.Combine(sourceDir, "sub1", "sub2"));
            File.WriteAllText(Path.Combine(sourceDir, "file1.txt"), "content1");
            File.WriteAllText(Path.Combine(sourceDir, "sub1", "file2.txt"), "content2");
            File.WriteAllText(Path.Combine(sourceDir, "sub1", "sub2", "file3.txt"), "content3");

            // 执行
            PluginUtilities.CopyDirectory(sourceDir, destDir);

            // 验证
            Assert.True(File.Exists(Path.Combine(destDir, "file1.txt")));
            Assert.True(File.Exists(Path.Combine(destDir, "sub1", "file2.txt")));
            Assert.True(File.Exists(Path.Combine(destDir, "sub1", "sub2", "file3.txt")));
            Assert.Equal("content1", File.ReadAllText(Path.Combine(destDir, "file1.txt")));
            Assert.Equal("content3", File.ReadAllText(Path.Combine(destDir, "sub1", "sub2", "file3.txt")));
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void CopyDirectory_OverwritesExistingFiles()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"lybox-test-{Guid.NewGuid():N}");
        var sourceDir = Path.Combine(tempRoot, "source");
        var destDir = Path.Combine(tempRoot, "dest");

        try
        {
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(destDir);

            File.WriteAllText(Path.Combine(sourceDir, "shared.txt"), "new_content");
            File.WriteAllText(Path.Combine(destDir, "shared.txt"), "old_content");

            PluginUtilities.CopyDirectory(sourceDir, destDir);

            Assert.Equal("new_content", File.ReadAllText(Path.Combine(destDir, "shared.txt")));
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void CopyDirectory_CreatesDestinationIfNotExists()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"lybox-test-{Guid.NewGuid():N}");
        var sourceDir = Path.Combine(tempRoot, "source");
        var destDir = Path.Combine(tempRoot, "dest");

        try
        {
            Directory.CreateDirectory(sourceDir);
            File.WriteAllText(Path.Combine(sourceDir, "file.txt"), "data");

            // 目标目录不存在
            Assert.False(Directory.Exists(destDir));

            PluginUtilities.CopyDirectory(sourceDir, destDir);

            Assert.True(Directory.Exists(destDir));
            Assert.True(File.Exists(Path.Combine(destDir, "file.txt")));
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
        }
    }

    #endregion
}
