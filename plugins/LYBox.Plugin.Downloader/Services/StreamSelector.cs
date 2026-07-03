using System.Globalization;
using System.Text.RegularExpressions;
using LYBox.Plugin.Downloader.Models;

namespace LYBox.Plugin.Downloader.Services;

/// <summary>
/// 流选择器：对应 N_m3u8DL-RE 的 -sv/-sa/-ss（选流）与 -dv/-da/-ds（去流）及 --auto-select。
/// 支持简化语法：
///   "best" / "worst" / "best2" / "worst3" / "all"
///   "res=1920*:codecs=hvc1:for=best"  (键值正则，键: res/codecs/lang/name/id/ch/frame/url)
/// </summary>
public static class StreamSelector
{
    /// <summary>对每种媒体类型应用选流规则，返回被选中的轨道</summary>
    public static List<StreamTrack> Select(
        List<StreamTrack> tracks,
        string? selectVideo,
        string? selectAudio,
        string? selectSubtitle,
        bool autoSelect,
        bool subOnly)
    {
        var result = new List<StreamTrack>();
        var videos = tracks.Where(t => t.MediaType == MediaType.Video).OrderByDescending(t => t.Bandwidth ?? 0).ToList();
        var audios = tracks.Where(t => t.MediaType == MediaType.Audio).OrderByDescending(t => t.Bandwidth ?? 0).ToList();
        var subs = tracks.Where(t => t.MediaType == MediaType.Subtitle).ToList();

        if (autoSelect)
        {
            if (videos.Count > 0) result.Add(videos[0]);
            if (audios.Count > 0) result.Add(audios[0]);
            if (subs.Count > 0) result.Add(subs[0]);
            return result;
        }

        // sub-only：仅保留字幕
        if (subOnly)
        {
            result.AddRange(ApplySpec(subs, selectSubtitle ?? string.Empty));
            return result;
        }

        if (!string.IsNullOrWhiteSpace(selectVideo) && videos.Count > 0)
            result.AddRange(ApplySpec(videos, selectVideo));
        else if (videos.Count > 0 && !string.IsNullOrWhiteSpace(selectAudio) is false && audios.Count == 0)
            result.Add(videos[0]); // 无显式规则时取最佳视频

        if (!string.IsNullOrWhiteSpace(selectAudio) && audios.Count > 0)
            result.AddRange(ApplySpec(audios, selectAudio));

        if (!string.IsNullOrWhiteSpace(selectSubtitle) && subs.Count > 0)
            result.AddRange(ApplySpec(subs, selectSubtitle ?? string.Empty));

        // 若一条都没选，回退到每类最佳
        if (result.Count == 0)
        {
            if (videos.Count > 0) result.Add(videos[0]);
            if (audios.Count > 0) result.Add(audios[0]);
        }
        return result;
    }

    /// <summary>应用 -dv/-da/-ds 去流规则</summary>
    public static List<StreamTrack> Drop(List<StreamTrack> tracks, string? dropVideo, string? dropAudio, string? dropSubtitle)
    {
        var result = tracks.ToList();
        if (!string.IsNullOrWhiteSpace(dropVideo))
            result.RemoveAll(t => t.MediaType == MediaType.Video && ApplySpec([t], dropVideo).Count > 0);
        if (!string.IsNullOrWhiteSpace(dropAudio))
            result.RemoveAll(t => t.MediaType == MediaType.Audio && ApplySpec([t], dropAudio).Count > 0);
        if (!string.IsNullOrWhiteSpace(dropSubtitle))
            result.RemoveAll(t => t.MediaType == MediaType.Subtitle && ApplySpec([t], dropSubtitle).Count > 0);
        return result;
    }

    private static List<StreamTrack> ApplySpec(List<StreamTrack> pool, string spec)
    {
        var s = spec.Trim();
        // 纯关键字
        if (TryParseFor(s, out var forKind, out var forN))
        {
            return PickByFor(pool, forKind, forN);
        }
        if (s.Equals("all", StringComparison.OrdinalIgnoreCase)) return pool;

        // 键值:for=...
        string? forClause = null;
        var parts = s.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     .Select(p => p.Trim()).ToList();
        var filtered = pool.AsEnumerable();
        foreach (var p in parts)
        {
            var kv = p.Split('=', 2, StringSplitOptions.TrimEntries);
            if (kv.Length != 2) continue;
            var key = kv[0].ToLowerInvariant();
            var val = kv[1];
            if (key == "for") { forClause = val; continue; }
            filtered = filtered.Where(t => MatchField(t, key, val));
        }
        var list = filtered.ToList();
        if (forClause is not null && TryParseFor(forClause, out var fk, out var fn))
            return PickByFor(list, fk, fn);
        return list.Count > 0 ? list : pool; // 匹配为空则保留原池（best-effort）
    }

    private static bool MatchField(StreamTrack t, string key, string val)
    {
        var field = key switch
        {
            "id" => t.Id,
            "lang" => t.Language,
            "name" => t.Name,
            "codecs" => t.Codecs,
            "res" => t.Resolution,
            "ch" => t.Channels?.ToString(),
            "frame" => t.FrameRate?.ToString(CultureInfo.InvariantCulture),
            "url" => t.Url,
            _ => null
        };
        if (field is null) return false;
        try
        {
            // val 可含 * 通配，转正则
            var pattern = "^" + Regex.Escape(val).Replace("\\*", ".*") + "$";
            return Regex.IsMatch(field, pattern, RegexOptions.IgnoreCase);
        }
        catch { return field.Contains(val, StringComparison.OrdinalIgnoreCase); }
    }

    private static bool TryParseFor(string s, out string kind, out int n)
    {
        kind = ""; n = 0;
        var lower = s.ToLowerInvariant();
        if (lower == "best") { kind = "best"; return true; }
        if (lower == "worst") { kind = "worst"; return true; }
        if (lower == "all") { kind = "all"; return true; }
        var m = Regex.Match(lower, @"^(best|worst)(\d+)$");
        if (m.Success) { kind = m.Groups[1].Value; n = int.Parse(m.Groups[2].Value); return true; }
        return false;
    }

    private static List<StreamTrack> PickByFor(List<StreamTrack> pool, string kind, int n)
    {
        var ordered = pool.OrderByDescending(t => t.Bandwidth ?? 0).ToList();
        return kind switch
        {
            "best" when n <= 0 => ordered.Take(1).ToList(),
            "best" => ordered.Take(n).ToList(),
            "worst" when n <= 0 => ordered.TakeLast(1).ToList(),
            "worst" => ordered.TakeLast(n).ToList(),
            "all" => pool,
            _ => ordered.Take(1).ToList()
        };
    }
}
