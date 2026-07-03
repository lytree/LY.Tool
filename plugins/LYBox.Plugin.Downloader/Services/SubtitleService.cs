using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using LYBox.Plugin.Downloader.Models;

namespace LYBox.Plugin.Downloader.Services;

/// <summary>
/// 字幕处理：对应 N_m3u8DL-RE 的 --sub-format（SRT/VTT）、--sub-only、--auto-subtitle-fix。
/// 将分段 VTT 合并为单文件，并提供 VTT→SRT 转换与时间戳修正。
/// </summary>
public static class SubtitleService
{
    /// <summary>合并多个 VTT 分片为一个完整 VTT 文本（带累计时间戳偏移）</summary>
    public static string MergeVttSegments(IEnumerable<string> segmentFiles, DirectUiLogger? logger = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("WEBVTT");
        sb.AppendLine();
        double offset = 0;
        foreach (var file in segmentFiles.OrderBy(f => int.TryParse(Path.GetFileNameWithoutExtension(f), out var n) ? n : 0))
        {
            if (!File.Exists(file)) continue;
            try
            {
                var text = File.ReadAllText(file);
                var (content, duration) = NormalizeVttBlock(text, offset);
                sb.Append(content);
                if (duration > 0) offset += duration;
            }
            catch (Exception ex) { logger?.Log($"[Subtitle] 合并失败 {Path.GetFileName(file)}: {ex.Message}"); }
        }
        return sb.ToString();
    }

    /// <summary>VTT → SRT 转换</summary>
    public static string VttToSrt(string vtt)
    {
        var sb = new StringBuilder();
        var lines = vtt.Replace("\r\n", "\n").Split('\n');
        int cueIndex = 1;
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var match = Regex.Match(line, @"(\d{2}:\d{2}:\d{2}\.\d{3})\s*-->\s*(\d{2}:\d{2}:\d{2}\.\d{3})");
            if (!match.Success)
            {
                // 简化时间戳格式 mm:ss.fff
                var m2 = Regex.Match(line, @"(\d{2}:\d{2}\.\d{3})\s*-->\s*(\d{2}:\d{2}\.\d{3})");
                if (m2.Success)
                {
                    sb.AppendLine(cueIndex.ToString());
                    sb.AppendLine($"00:{m2.Groups[1].Value} --> 00:{m2.Groups[2].Value}".Replace('.', ','));
                    cueIndex++;
                    // 收集后续正文行
                    for (int j = i + 1; j < lines.Length; j++)
                    {
                        if (string.IsNullOrWhiteSpace(lines[j])) { i = j; break; }
                        if (Regex.IsMatch(lines[j], @"\d{2}:\d{2}")) { i = j - 1; break; }
                        sb.AppendLine(lines[j]);
                    }
                    sb.AppendLine();
                }
                continue;
            }
            sb.AppendLine(cueIndex.ToString());
            sb.AppendLine($"{match.Groups[1].Value} --> {match.Groups[2].Value}".Replace('.', ','));
            cueIndex++;
            for (int j = i + 1; j < lines.Length; j++)
            {
                if (string.IsNullOrWhiteSpace(lines[j])) { i = j; break; }
                if (Regex.IsMatch(lines[j], @"\d{2}:\d{2}:\d{2}")) { i = j - 1; break; }
                sb.AppendLine(lines[j]);
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    /// <summary>auto-subtitle-fix：将首条 cue 起点对齐到 0（向后平移）</summary>
    public static string AutoFixTimestamps(string vtt)
    {
        var first = Regex.Match(vtt, @"(\d{2}:\d{2}:\d{2}\.\d{3})\s*-->");
        if (!first.Success) return vtt;
        var startTs = ParseVttTimestamp(first.Groups[1].Value);
        if (startTs <= 0) return vtt;
        return Regex.Replace(vtt, @"(\d{2}:\d{2}:\d{2}\.\d{3})", m =>
            FormatVttTimestamp(Math.Max(0, ParseVttTimestamp(m.Groups[1].Value) - startTs)));
    }

    private static (string content, double lastDuration) NormalizeVttBlock(string text, double offset)
    {
        var sb = new StringBuilder();
        var lines = text.Replace("\r\n", "\n").Split('\n');
        double lastEnd = 0;
        foreach (var line in lines)
        {
            var m = Regex.Match(line, @"(\d{2}:\d{2}:\d{2}\.\d{3})\s*-->\s*(\d{2}:\d{2}:\d{2}\.\d{3})");
            if (m.Success)
            {
                var s = ParseVttTimestamp(m.Groups[1].Value) + offset;
                var e = ParseVttTimestamp(m.Groups[2].Value) + offset;
                lastEnd = e;
                sb.AppendLine($"{FormatVttTimestamp(s)} --> {FormatVttTimestamp(e)}");
            }
            else if (!line.StartsWith("WEBVTT", StringComparison.OrdinalIgnoreCase) &&
                     !string.IsNullOrWhiteSpace(line))
            {
                sb.AppendLine(line);
            }
            else if (string.IsNullOrWhiteSpace(line))
            {
                sb.AppendLine();
            }
        }
        return (sb.ToString(), lastEnd - offset);
    }

    private static double ParseVttTimestamp(string ts)
    {
        var parts = ts.Split([':', '.']);
        if (parts.Length != 4) return 0;
        double.TryParse(parts[0], out var h);
        double.TryParse(parts[1], out var m);
        double.TryParse(parts[2], out var s);
        double.TryParse(parts[3], out var ms);
        return h * 3600 + m * 60 + s + ms / 1000.0;
    }

    private static string FormatVttTimestamp(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{(int)(ts.Milliseconds):D3}";
    }
}
