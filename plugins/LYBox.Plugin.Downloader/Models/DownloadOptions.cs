namespace LYBox.Plugin.Downloader.Models;

/// <summary>
/// 下载选项集合，对应 N_m3u8DL-RE 命令行参数的 C# 表示。
/// 由各页面 ViewModel 组装，传给下载编排器。
/// </summary>
public class DownloadOptions
{
    // ===== 输入与输出 =====
    public string Input { get; set; } = string.Empty;
    public string? SaveDir { get; set; }
    public string? SaveName { get; set; }
    public string? SavePattern { get; set; }   // --save-pattern

    // ===== 下载控制 =====
    public int ThreadCount { get; set; } = 8;
    public int RetryCount { get; set; } = 3;
    public int HttpRequestTimeout { get; set; } = 100;     // 秒
    public Dictionary<string, string> Headers { get; set; } = new();
    public string? Proxy { get; set; }                     // --custom-proxy
    public bool UseSystemProxy { get; set; } = true;
    public string? MaxSpeed { get; set; }                  // --max-speed, 形如 "15M" "100K"
    public string? CustomRange { get; set; }               // --custom-range

    // ===== 选流 =====
    public bool AutoSelect { get; set; }
    public string? SelectVideo { get; set; }               // -sv
    public string? SelectAudio { get; set; }               // -sa
    public string? SelectSubtitle { get; set; }            // -ss
    public bool SubOnly { get; set; }                      // --sub-only
    public string SubFormat { get; set; } = "SRT";         // --sub-format
    public bool AutoSubtitleFix { get; set; } = true;

    // ===== 合并 =====
    public bool SkipMerge { get; set; }
    public bool SkipDownload { get; set; }
    public bool BinaryMerge { get; set; }
    public bool UseFfmpegConcatDemuxer { get; set; }
    public bool DelAfterDone { get; set; } = true;
    public bool CheckSegmentsCount { get; set; } = true;
    public bool NoDateInfo { get; set; }
    public bool WriteMetaJson { get; set; } = true;

    // ===== 解密 =====
    /// <summary>由解析器填充的轨道加密信息（运行时设置，非 UI 参数）</summary>
    public EncryptionInfo? Encryption { get; set; }
    public List<DecryptionKey> Keys { get; set; } = new();   // --key KID:KEY
    public string? KeyTextFile { get; set; }                 // --key-text-file
    public DecryptionEngine DecryptionEngine { get; set; } = DecryptionEngine.Mp4Decrypt;
    public bool Mp4RealTimeDecryption { get; set; }
    public string? CustomHlsMethod { get; set; }             // --custom-hls-method
    public string? CustomHlsKey { get; set; }                // FILE|HEX|BASE64
    public string? CustomHlsIv { get; set; }                 // FILE|HEX|BASE64

    // ===== 混流 =====
    public MuxOptions? MuxAfterDone { get; set; }            // -M
    public List<MuxImport> MuxImports { get; set; } = new(); // --mux-import

    // ===== 直播 =====
    public bool LivePerformAsVod { get; set; }
    public bool LiveRealTimeMerge { get; set; }
    public bool LiveKeepSegments { get; set; } = true;
    public bool LivePipeMux { get; set; }
    public bool LiveFixVttByAudio { get; set; }
    public TimeSpan? LiveRecordLimit { get; set; }
    public int? LiveWaitTime { get; set; }
    public int LiveTakeCount { get; set; } = 16;

    // ===== 外部二进制路径（来自设置页）=====
    public string FfmpegPath { get; set; } = "ffmpeg";
    public string MkvmergePath { get; set; } = "mkvmerge";
    public string? Mp4DecryptPath { get; set; }
    public string? ShakaPackagerPath { get; set; }
    public string LogLevel { get; set; } = "INFO";
}

/// <summary>混流配置（对应 -M/--mux-after-done）</summary>
public class MuxOptions
{
    public string Format { get; set; } = "mp4";       // mkv / mp4
    public string Muxer { get; set; } = "ffmpeg";     // ffmpeg / mkvmerge
    public string? BinPath { get; set; }
    public bool SkipSub { get; set; }
    public bool Keep { get; set; }
}

/// <summary>混流时引入的外部媒体（对应 --mux-import）</summary>
public class MuxImport
{
    public string Path { get; set; } = string.Empty;
    public string? Lang { get; set; }
    public string? Name { get; set; }
}
