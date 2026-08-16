using LYBox.Plugin.Shared;
using LYBox.Layout.Core.Services;
using TUnit.Core;
using TUnit.Assertions;

namespace LYBox.Tests;

/// <summary>
/// PluginLoader.IsPluginSdkCompatible 的单元测试。
/// 宿主 SDK 版本由 PluginSdkContract.CurrentVersion 编译时注入（当前为 "2.3.0-preview.3"，剥离预发布后为 2.3.0）。
/// </summary>
public class PluginLoaderSdkCompatibilityTests
{
    #region null / 空字符串 → 通过（无约束）

    [Test]
    [MethodDataSource(nameof(NullOrWhitespaceCases))]
    public async Task IsPluginSdkCompatible_NullOrWhitespace_ReturnsTrue(string? required)
    {
        await Assert.That(PluginLoader.IsPluginSdkCompatible(required)).IsTrue();
    }

    public static IEnumerable<object?[]> NullOrWhitespaceCases() => new List<object?[]>
    {
        new object?[] { null },
        new object?[] { "" },
        new object?[] { "   " },
        new object?[] { "\t" },
    };

    #endregion

    #region 解析失败 → 拒绝（fail-closed）

    [Test]
    [MethodDataSource(nameof(InvalidVersionCases))]
    public async Task IsPluginSdkCompatible_InvalidVersion_ReturnsFalse(string? required)
    {
        await Assert.That(PluginLoader.IsPluginSdkCompatible(required)).IsFalse();
    }

    public static IEnumerable<object[]> InvalidVersionCases() => new List<object[]>
    {
        new object[] { "abc" },       // 非数字
        new object[] { "1.x.0" },     // 次版本号非数字
        new object[] { "1.2.x" },     // 修订号非数字
        new object[] { "v2.1.0" },    // 带前缀
        new object[] { "2.1.0." },    // 尾部多余点
    };

    #endregion

    #region 主版本号不匹配 → 不兼容

    [Test]
    [MethodDataSource(nameof(MajorMismatchCases))]
    public async Task IsPluginSdkCompatible_MajorMismatch_ReturnsFalse(string required)
    {
        await Assert.That(PluginLoader.IsPluginSdkCompatible(required)).IsFalse();
    }

    public static IEnumerable<object[]> MajorMismatchCases() => new List<object[]>
    {
        new object[] { "1.1.0" },     // 低于主版本
        new object[] { "3.1.0" },     // 高于主版本
        new object[] { "1.0.0" },     // 最低版本
        new object[] { "10.0.0" },    // 远高版本
    };

    #endregion

    #region 主版本匹配，次版本号 > 要求 → 兼容

    [Test]
    [MethodDataSource(nameof(MinorHigherCases))]
    public async Task IsPluginSdkCompatible_MinorHigherThanRequired_ReturnsTrue(string required)
    {
        await Assert.That(PluginLoader.IsPluginSdkCompatible(required)).IsTrue();
    }

    public static IEnumerable<object[]> MinorHigherCases() => new List<object[]>
    {
        new object[] { "2.0.0" },     // 宿主次版本更高
        new object[] { "2.0.5" },      // 宿主次版本更高，修订号也更
        new object[] { "2.0.999" },    // 任意高修订号
    };

    #endregion

    #region 主版本匹配，次版本号 < 要求 → 不兼容

    [Test]
    [MethodDataSource(nameof(MinorLowerCases))]
    public async Task IsPluginSdkCompatible_MinorLowerThanRequired_ReturnsFalse(string required)
    {
        await Assert.That(PluginLoader.IsPluginSdkCompatible(required)).IsFalse();
    }

    public static IEnumerable<object[]> MinorLowerCases() => new List<object[]>
    {
        new object[] { "2.4.0" },     // 宿主次版本(3) < 要求(4) → 不兼容
        new object[] { "2.6.0" },     // 远高次版本
        new object[] { "2.5.0" },      // 次版本更高 + 修订号更高
    };

    #endregion

    #region 主版本+次版本匹配，修订号 >= 要求 → 兼容

    [Test]
    [MethodDataSource(nameof(ExactMatchCases))]
    public async Task IsPluginSdkCompatible_ExactMatch_ReturnsTrue(string required)
    {
        await Assert.That(PluginLoader.IsPluginSdkCompatible(required)).IsTrue();
    }

    public static IEnumerable<object[]> ExactMatchCases() => new List<object[]>
    {
        new object[] { "2.1.0" },     // 精确匹配
        new object[] { "2.1" },       // 缺省修订号 → 0
    };

    #endregion

    #region 主版本+次版本匹配，修订号 < 要求 → 不兼容

    [Test]
    [MethodDataSource(nameof(BuildHigherCases))]
    public async Task IsPluginSdkCompatible_BuildHigherThanRequired_ReturnsFalse(string required)
    {
        await Assert.That(PluginLoader.IsPluginSdkCompatible(required)).IsFalse();
    }

    public static IEnumerable<object[]> BuildHigherCases() => new List<object[]>
    {
        new object[] { "2.3.1" },     // 同主+次版本，修订号更高 → 不兼容
        new object[] { "2.3.999" },   // 远高修订号
    };

    #endregion

    #region 预发布标签 → 忽略，取稳定版本部分

    [Test]
    [MethodDataSource(nameof(PreReleaseStrippedAndMatchesCases))]
    public async Task IsPluginSdkCompatible_PreReleaseTag_StrippedAndMatches(string required)
    {
        // 预发布标签应被忽略，取 "2.1.0" 比较 → 兼容
        await Assert.That(PluginLoader.IsPluginSdkCompatible(required)).IsTrue();
    }

    public static IEnumerable<object[]> PreReleaseStrippedAndMatchesCases() => new List<object[]>
    {
        new object[] { "2.1.0-preview" },
        new object[] { "2.1.0-alpha.1" },
        new object[] { "2.1.0-rc.2+build.456" },
    };

    [Test]
    [MethodDataSource(nameof(PreReleaseStrippedAndRejectsWhenHigherCases))]
    public async Task IsPluginSdkCompatible_PreReleaseTag_StrippedAndRejectsWhenHigher(string required)
    {
        // 预发布标签被忽略后，核心版本高于宿主 → 不兼容
        await Assert.That(PluginLoader.IsPluginSdkCompatible(required)).IsFalse();
    }

    public static IEnumerable<object[]> PreReleaseStrippedAndRejectsWhenHigherCases() => new List<object[]>
    {
        new object[] { "2.3.1-preview" },  // 剥离后 2.3.1 > 宿主 2.3.0 → 不兼容
        new object[] { "2.4.0-beta" },     // 剥离后 2.4.0 > 宿主 2.3.0 → 不兼容
    };

    #endregion

    #region 缺省版本段

    [Test]
    [MethodDataSource(nameof(PartialVersionCases))]
    public async Task IsPluginSdkCompatible_PartialVersion_DefaultsMissingSegments(string required)
    {
        await Assert.That(PluginLoader.IsPluginSdkCompatible(required)).IsTrue();
    }

    public static IEnumerable<object[]> PartialVersionCases() => new List<object[]>
    {
        new object[] { "2" },     // 仅主版本 → 2.0.0，宿主更高 → 兼容
        new object[] { "2.1" },   // 主+次 → 2.1.0，精确匹配 → 兼容
    };

    #endregion
}
