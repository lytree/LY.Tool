using System.Globalization;

namespace LYBox.Plugin.Downloader.Services;

/// <summary>
/// 全局带宽限速器（令牌桶）。对应 N_m3u8DL-RE -R/--max-speed（如 "15M"=15Mbps）。
/// bytesPerSecond<=0 表示不限速。
/// </summary>
public sealed class BandwidthThrottle
{
    private readonly long _bytesPerSecond;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private double _tokens;
    private long _lastRefillTicks;

    public BandwidthThrottle(long bytesPerSecond)
    {
        _bytesPerSecond = bytesPerSecond;
        _tokens = bytesPerSecond;
        _lastRefillTicks = Environment.TickCount64;
    }

    /// <summary>解析 "15M"/"100K"/"1500000" 为字节/秒（M/K 为比特每秒，需 /8）</summary>
    public static long? ParseSpeed(string? speed)
    {
        if (string.IsNullOrWhiteSpace(speed)) return null;
        var s = speed.Trim().ToUpperInvariant();
        double multiplier;
        bool bits;
        if (s.EndsWith('M')) { multiplier = 1_000_000; bits = true; s = s[..^1]; }
        else if (s.EndsWith('K')) { multiplier = 1_000; bits = true; s = s[..^1]; }
        else { multiplier = 1; bits = false; }
        if (!double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return null;
        var bps = v * multiplier;
        return bits ? (long)(bps / 8.0) : (long)bps;
    }

    public async Task WaitForBytesAsync(int count, CancellationToken ct)
    {
        if (_bytesPerSecond <= 0) return;
        while (count > 0)
        {
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                Refill();
                var take = (int)Math.Min(count, Math.Max(1, _tokens));
                _tokens -= take;
                count -= take;
                if (count > 0)
                {
                    // 令牌不足，等待补充
                    var deficit = count;
                    var waitMs = (int)Math.Ceiling(deficit * 1000.0 / _bytesPerSecond);
                    waitMs = Math.Clamp(waitMs, 1, 500);
                    _gate.Release();
                    await Task.Delay(waitMs, ct).ConfigureAwait(false);
                    continue;
                }
            }
            finally
            {
                if (_gate.CurrentCount == 0) _gate.Release();
            }
        }
    }

    private void Refill()
    {
        var now = Environment.TickCount64;
        var elapsed = now - _lastRefillTicks;
        if (elapsed <= 0) return;
        _tokens = Math.Min(_bytesPerSecond, _tokens + elapsed * (_bytesPerSecond / 1000.0));
        _lastRefillTicks = now;
    }
}
