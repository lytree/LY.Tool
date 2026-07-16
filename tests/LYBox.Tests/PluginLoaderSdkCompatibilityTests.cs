using LYBox.Plugin.Shared;
using LYBox.UrsaWindow.Services;
using Xunit;

namespace LYBox.Tests;

/// <summary>
/// PluginLoader.IsPluginSdkCompatible 的单元测试。
/// 宿主 SDK 版本由 PluginSdkContract.CurrentVersion 编译时注入（当前为 "2.1.0"）。
/// </summary>
public class PluginLoaderSdkCompatibilityTests
{
    // 当前宿主版本：HostVersion=2.1.0 → PluginSdkContract.CurrentVersion="2.1.0"
    private const string HostVersion = "2.1.0";

    #region null / 空字符串 → 通过（无约束）

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void IsPluginSdkCompatible_NullOrWhitespace_ReturnsTrue(string? required)
    {
        Assert.True(PluginLoader.IsPluginSdkCompatible(required));
    }

    #endregion

    #region 解析失败 → 拒绝（fail-closed）

    [Theory]
    [InlineData("abc")]           // 非数字
    [InlineData("1.x.0")]         // 次版本号非数字
    [InlineData("1.2.x")]          // 修订号非数字
    [InlineData("v2.1.0")]        // 带前缀
    [InlineData("2.1.0.")]         // 尾部多余点
    public void IsPluginSdkCompatible_InvalidVersion_ReturnsFalse(string? required)
    {
        Assert.False(PluginLoader.IsPluginSdkCompatible(required));
    }

    #endregion

    #region 主版本号不匹配 → 不兼容

    [Theory]
    [InlineData("1.1.0")]         // 低于主版本
    [InlineData("3.1.0")]         // 高于主版本
    [InlineData("1.0.0")]         // 最低版本
    [InlineData("10.0.0")]        // 远高版本
    public void IsPluginSdkCompatible_MajorMismatch_ReturnsFalse(string required)
    {
        Assert.False(PluginLoader.IsPluginSdkCompatible(required));
    }

    #endregion

    #region 主版本匹配，次版本号 > 要求 → 兼容

    [Theory]
    [InlineData("2.0.0")]         // 宿主次版本更高
    [InlineData("2.0.5")]          // 宿主次版本更高，修订号也更
    [InlineData("2.0.999")]       // 任意高修订号
    public void IsPluginSdkCompatible_MinorHigherThanRequired_ReturnsTrue(string required)
    {
        Assert.True(PluginLoader.IsPluginSdkCompatible(required));
    }

    #endregion

    #region 主版本匹配，次版本号 < 要求 → 不兼容

    [Theory]
    [InlineData("2.2.0")]         // 宿主次版本更低
    [InlineData("2.5.0")]         // 远高次版本
    [InlineData("2.2.1")]          // 次版本+修订号都更高
    public void IsPluginSdkCompatible_MinorLowerThanRequired_ReturnsFalse(string required)
    {
        Assert.False(PluginLoader.IsPluginSdkCompatible(required));
    }

    #endregion

    #region 主版本+次版本匹配，修订号 >= 要求 → 兼容

    [Theory]
    [InlineData("2.1.0")]         // 精确匹配
    [InlineData("2.1")]           // 缺省修订号 → 0
    public void IsPluginSdkCompatible_ExactMatch_ReturnsTrue(string required)
    {
        Assert.True(PluginLoader.IsPluginSdkCompatible(required));
    }

    #endregion

    #region 主版本+次版本匹配，修订号 < 要求 → 不兼容

    [Theory]
    [InlineData("2.1.1")]         // 修订号更高
    [InlineData("2.1.999")]       // 远高修订号
    public void IsPluginSdkCompatible_BuildHigherThanRequired_ReturnsFalse(string required)
    {
        Assert.False(PluginLoader.IsPluginSdkCompatible(required));
    }

    #endregion

    #region 预发布标签 → 忽略，取稳定版本部分

    [Theory]
    [InlineData("2.1.0-preview")]
    [InlineData("2.1.0-alpha.1")]
    [InlineData("2.1.0-rc.2+build.456")]
    public void IsPluginSdkCompatible_PreReleaseTag_StrippedAndMatches(string required)
    {
        // 预发布标签应被忽略，取 "2.1.0" 比较 → 兼容
        Assert.True(PluginLoader.IsPluginSdkCompatible(required));
    }

    [Theory]
    [InlineData("2.2.0-preview")]
    [InlineData("2.1.5-beta")]
    public void IsPluginSdkCompatible_PreReleaseTag_StrippedAndRejectsWhenHigher(string required)
    {
        // 预发布标签被忽略后，核心版本高于宿主 → 不兼容
        Assert.False(PluginLoader.IsPluginSdkCompatible(required));
    }

    #endregion

    #region 缺省版本段

    [Theory]
    [InlineData("2")]             // 仅主版本 → 2.0.0，宿主更高 → 兼容
    [InlineData("2.1")]           // 主+次 → 2.1.0，精确匹配 → 兼容
    public void IsPluginSdkCompatible_PartialVersion_DefaultsMissingSegments(string required)
    {
        Assert.True(PluginLoader.IsPluginSdkCompatible(required));
    }

    #endregion
}
