using System;

namespace LYBox.Plugin.BTSou.Models;

/// <summary>
/// 授权锁信息（lockchange 表）。
/// 上锁码 0 表示未授权；5-8 为随机生成的上锁码。
/// </summary>
public class LockInfo
{
    /// <summary>硬盘序列号（本机标识）</summary>
    public string HardDiskSerial { get; set; } = "";

    /// <summary>上锁码（0=未授权）</summary>
    public int LockCode { get; set; }

    /// <summary>更新日期</summary>
    public DateTime UpdateTime { get; set; }

    /// <summary>是否已授权</summary>
    public bool IsLicensed => LockCode != 0;
}

/// <summary>
/// 举报信息（reportdata 表）。
/// </summary>
public class ReportEntry
{
    public string HardDiskSerial { get; set; } = "";
    public string Title { get; set; } = "";
    public string MagnetLink { get; set; } = "";
    public DateTime SubmitTime { get; set; } = DateTime.Now;
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string IdCard { get; set; } = "";
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary>
    /// 校验：邮箱或手机号至少填一项；危害类别必选；详细描述必填。
    /// </summary>
    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(Email) && string.IsNullOrWhiteSpace(Phone))
            return "请正确填写邮箱和手机号(至少填写一项)。";
        if (string.IsNullOrWhiteSpace(Category))
            return "请正确勾选危害类别。";
        if (string.IsNullOrWhiteSpace(Description))
            return "请填写详细描述，有助于快速定位违法信息。";
        return null;
    }
}
