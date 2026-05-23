namespace Avalonia.Plugin.Downloader.Models;

public record M3u8Info(List<TsSegment> Segments, EncryptInfo? KeyInfo);

public record TsSegment(int Index, string Url, double? Duration);

public record EncryptInfo(string Method, string KeyUrl, string? Iv);

public record StreamInfo(long Bandwidth, string Resolution, string Codecs, string Url);
