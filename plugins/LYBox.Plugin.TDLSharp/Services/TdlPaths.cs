namespace LYBox.Plugin.TDLSharp.Services;

/// <summary>
/// 集中管理插件默认数据目录。所有脚本输出文件均落在 <see cref="DataRoot"/> 之下，
/// 避免在发布目录只读场景下写入失败。
/// </summary>
public static class TdlPaths
{
    public static string DataRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AvaloniaTemplate", "TDLSharp");

    public static string DataSubdir(string leaf) => Path.Combine(DataRoot, leaf);

    /// <summary>默认下载目录（脚本未指定输出目录时使用）。</summary>
    public static string DefaultDownloadDir => DataSubdir("download");

    /// <summary>默认消息导出目录（脚本未指定输出目录时使用）。</summary>
    public static string DefaultExportDir => DataSubdir("message");

    /// <summary>默认聊天列表导出目录。</summary>
    public static string DefaultChatsDir => DataSubdir("chats");

    /// <summary>默认成员列表导出目录。</summary>
    public static string DefaultMembersDir => DataSubdir("members");

    /// <summary>默认转发记录数据库目录（每个 source chat 独立一个 db 文件）。</summary>
    public static string ForwardDbDir => DataSubdir("data");

    /// <summary>执行历史数据库目录（每个 script 独立一个 db 文件）。</summary>
    public static string HistoryDir => DataSubdir("history");

    /// <summary>将任意字符串清洗为文件系统安全的文件名前缀。</summary>
    public static string SafeFileName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "_";
        var buffer = new char[raw.Length];
        var len = 0;
        foreach (var c in raw)
        {
            buffer[len++] = char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '_';
        }
        return new string(buffer, 0, len);
    }
}
