namespace LYBox.Plugin.Downloader.Models;

/// <summary>媒体类型</summary>
public enum MediaType { Video, Audio, Subtitle }

/// <summary>媒体清单种类（对应输入源解析结果）</summary>
public enum MediaKind { HlsMaster, HlsMedia, Dash, Mss }

/// <summary>解密引擎</summary>
public enum DecryptionEngine { Ffmpeg, Mp4Decrypt, ShakaPackager }

/// <summary>
/// 可选流轨道（来自 master playlist / MPD AdaptationSet）。
/// 对应 N_m3u8DL-RE 的 -sv/-sa/-ss 选择对象。
/// </summary>
public record StreamTrack(
    MediaType MediaType,
    string Url,
    string? Id = null,
    string? Language = null,
    string? Name = null,
    string? Codecs = null,
    string? Resolution = null,
    long? Bandwidth = null,
    int? Channels = null,
    double? FrameRate = null,
    string? VideoRange = null,
    string? GroupId = null,
    int? SegmentCount = null,
    double? PlaylistDurationSeconds = null)
{
    public override string ToString()
    {
        var label = MediaType switch
        {
            MediaType.Video => Resolution ?? Codecs ?? $"{Bandwidth / 1000.0:F0}kbps",
            MediaType.Audio => $"{Language ?? Name ?? Codecs}{(Channels is > 0 ? $" {Channels}ch" : "")}",
            MediaType.Subtitle => $"{Language ?? Name ?? "subtitle"}",
            _ => Url
        };
        return $"[{MediaType}] {label}";
    }
}

/// <summary>媒体分片</summary>
public record MediaSegment(
    int Index,
    string Url,
    double? DurationSeconds = null,
    bool IsDiscontinuity = false,
    string? InitUrl = null)
{
    public byte[]? InitData { get; set; }
}

/// <summary>加密信息（HLS EXT-X-KEY / DASH ContentProtection）</summary>
public record EncryptionInfo(
    string Method,          // AES-128, AES-128-ECB, CHACHA20, CENC, SAMPLE-AES, NONE, UNKNOWN
    string? KeyUrl = null,
    string? IvHex = null,
    string? Kid = null,     // CENC KID
    string? KeyFormat = null)
{
    public byte[]? Key { get; set; }
}

/// <summary>用户提供的解密密钥（--key KID:KEY）</summary>
public record DecryptionKey(string? Kid, byte[] Key);

/// <summary>解析输入源得到的结果</summary>
public record ParsedMedia(
    string InputUrl,
    MediaKind Kind,
    bool IsLive,
    List<StreamTrack> Tracks,
    EncryptionInfo? Encryption = null);
