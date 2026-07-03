using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using CliWrap;
using LYBox.Plugin.Downloader.Models;

namespace LYBox.Plugin.Downloader.Services;

/// <summary>
/// 解密提供器：对应 N_m3u8DL-RE 的解密能力。
/// - AES-128/CBC、AES-128-ECB、CHACHA20：内存解密（内置实现）
/// - CENC：调用外部 mp4decrypt / shaka-packager（文件级解密）
/// - 自定义 HLS key/iv（--custom-hls-key / --custom-hls-iv）：覆盖从清单取到的密钥
/// </summary>
public class DecryptionProvider : IDecryptionProvider
{
    private readonly DirectUiLogger _logger;
    private readonly DownloadOptions _opts;
    private readonly byte[]? _overrideKey;
    private readonly byte[]? _overrideIv;

    public bool NeedsFileBasedDecryption => _opts.Encryption?.Method is "CENC";

    public DecryptionProvider(DirectUiLogger logger, DownloadOptions opts)
    {
        _logger = logger;
        _opts = opts;
        _overrideKey = ParseKeyMaterial(opts.CustomHlsKey);
        _overrideIv = ParseKeyMaterial(opts.CustomHlsIv);
    }

    /// <summary>内存解密单个分片（AES-128/CBC、AES-128-ECB、CHACHA20）</summary>
    public byte[] DecryptSegment(byte[] encrypted, int segmentIndex, byte[]? initData)
    {
        var enc = _opts.Encryption;
        if (enc is null || enc.Method is "NONE" or null) return encrypted;

        var method = _opts.CustomHlsMethod ?? enc.Method;
        var key = _overrideKey ?? enc.Key ?? FindUserKey(enc.Kid);

        if (method is "CENC") return encrypted; // CENC 走文件级

        if (key is null)
        {
            _logger.Log($"[Decrypt] 无可用密钥，分片 #{segmentIndex} 未解密");
            return encrypted;
        }

        return method.ToUpperInvariant() switch
        {
            "AES-128-ECB" => AesDecrypt(encrypted, key, null, CipherMode.ECB),
            "AES-128" => AesDecrypt(encrypted, key, ResolveIv(enc.IvHex, segmentIndex), CipherMode.CBC),
            "CHACHA20" => ChaCha20Decrypt(encrypted, key, ResolveIv(enc.IvHex, segmentIndex)),
            "SAMPLE-AES" => throw new NotSupportedException("SAMPLE-AES (FairPlay) 需要许可证服务器"),
            _ => encrypted
        };
    }

    /// <summary>文件级解密（CENC）：用 mp4decrypt / shaka-packager 原地解密</summary>
    public void DecryptFile(string filePath)
    {
        var enc = _opts.Encryption;
        if (enc is null || enc.Method is not "CENC") return;

        var keys = CollectKeys(enc.Kid);
        if (keys.Count == 0)
        {
            _logger.Log($"[CENC] 未提供密钥，跳过: {Path.GetFileName(filePath)}");
            return;
        }

        var tmpOut = filePath + ".dec";
        try
        {
            if (_opts.DecryptionEngine == DecryptionEngine.ShakaPackager)
                RunShaka(filePath, tmpOut, keys);
            else
                RunMp4Decrypt(filePath, tmpOut, keys);

            File.Move(tmpOut, filePath, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.Log($"[CENC] 解密失败 {Path.GetFileName(filePath)}: {ex.Message}");
            if (File.Exists(tmpOut)) try { File.Delete(tmpOut); } catch { }
        }
    }

    private void RunMp4Decrypt(string input, string output, List<DecryptionKey> keys)
    {
        var bin = _opts.Mp4DecryptPath ?? "mp4decrypt";
        var args = new StringBuilder();
        foreach (var k in keys)
        {
            var kid = k.Kid ?? "";
            args.Append($" --key {kid}:{Convert.ToHexString(k.Key).ToLowerInvariant()}");
        }
        args.Append($" \"{input}\" \"{output}\"");

        var result = Cli.Wrap(bin).WithArguments(args.ToString())
            .WithStandardErrorPipe(PipeTarget.ToDelegate(l => { if (!string.IsNullOrEmpty(l)) _logger.Log($"[mp4decrypt] {l}"); }))
            .ExecuteAsync().GetAwaiter().GetResult();
        if (result.ExitCode != 0) throw new Exception($"mp4decrypt 退出码 {result.ExitCode}");
    }

    private void RunShaka(string input, string output, List<DecryptionKey> keys)
    {
        var bin = _opts.ShakaPackagerPath ?? "shaka-packager";
        var sb = new StringBuilder();
        sb.Append("--enable_raw_key_decryption");
        foreach (var k in keys)
        {
            var kid = k.Kid ?? "";
            sb.Append($" --key {kid}:{Convert.ToHexString(k.Key).ToLowerInvariant()}");
        }
        sb.Append($" input=\"{input}\",output=\"{output}\"");

        var result = Cli.Wrap(bin).WithArguments(sb.ToString())
            .WithStandardErrorPipe(PipeTarget.ToDelegate(l => { if (!string.IsNullOrEmpty(l)) _logger.Log($"[shaka] {l}"); }))
            .ExecuteAsync().GetAwaiter().GetResult();
        if (result.ExitCode != 0) throw new Exception($"shaka-packager 退出码 {result.ExitCode}");
    }

    private List<DecryptionKey> CollectKeys(string? fallbackKid)
    {
        var list = _opts.Keys.ToList();
        if (_overrideKey is not null && !list.Any(k => k.Key.SequenceEqual(_overrideKey)))
            list.Add(new DecryptionKey(fallbackKid, _overrideKey));
        return list;
    }

    private byte[]? FindUserKey(string? kid)
    {
        if (kid is null) return _opts.Keys.FirstOrDefault()?.Key;
        return _opts.Keys.FirstOrDefault(k => k.Kid?.Equals(kid, StringComparison.OrdinalIgnoreCase) == true)?.Key
               ?? _opts.Keys.FirstOrDefault()?.Key;
    }

    private static byte[]? ParseKeyMaterial(string? spec)
    {
        if (string.IsNullOrWhiteSpace(spec)) return null;
        try
        {
            if (spec.StartsWith("FILE:", StringComparison.OrdinalIgnoreCase))
                return File.ReadAllBytes(spec[5..]);
            if (spec.StartsWith("HEX:", StringComparison.OrdinalIgnoreCase))
                return Convert.FromHexString(spec[4..]);
            if (spec.StartsWith("BASE64:", StringComparison.OrdinalIgnoreCase))
                return Convert.FromBase64String(spec[7..]);
            // 裸 HEX
            if (spec.Length % 2 == 0) return Convert.FromHexString(spec);
        }
        catch { /* 解析失败返回 null */ }
        return null;
    }

    private static byte[] ResolveIv(string? ivHex, int segmentIndex)
    {
        if (!string.IsNullOrEmpty(ivHex))
        {
            var iv = new byte[16];
            var hex = ivHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? ivHex[2..] : ivHex;
            var bytes = Convert.FromHexString(hex);
            var offset = 16 - bytes.Length;
            Array.Copy(bytes, 0, iv, offset, bytes.Length);
            return iv;
        }
        // 默认 IV = segment index（大端 16 字节）
        var ivDefault = new byte[16];
        var idxBytes = BitConverter.GetBytes(segmentIndex);
        if (BitConverter.IsLittleEndian) Array.Reverse(idxBytes);
        Array.Copy(idxBytes, 0, ivDefault, 12, 4);
        return ivDefault;
    }

    private static byte[] AesDecrypt(byte[] data, byte[] key, byte[]? iv, CipherMode mode)
    {
        using var aes = Aes.Create();
        aes.BlockSize = 128;
        aes.KeySize = key.Length * 8;
        aes.Key = key;
        aes.IV = iv ?? new byte[16];
        aes.Mode = mode;
        aes.Padding = PaddingMode.PKCS7;
        using var decryptor = aes.CreateDecryptor();
        using var msIn = new MemoryStream(data);
        using var cs = new CryptoStream(msIn, decryptor, CryptoStreamMode.Read);
        using var msOut = new MemoryStream();
        cs.CopyTo(msOut);
        return msOut.ToArray();
    }

    /// <summary>ChaCha20 解密（HLS EXT-X-KEY:METHOD=CHACHA20，IV 作为 12 字节 nonce + 4 字节 counter）</summary>
    private static byte[] ChaCha20Decrypt(byte[] ciphertext, byte[] key, byte[] iv)
    {
        var state = new uint[16];
        var work = new uint[16];
        var block = new byte[64];
        var output = new byte[ciphertext.Length];

        state[0] = 0x61707865; state[1] = 0x3320646e; state[2] = 0x79622d32; state[3] = 0x6b206574;
        for (int i = 0; i < 8 && i * 4 < key.Length; i++)
            state[4 + i] = BitConverter.ToUInt32(key, i * 4);
        for (int i = 0; i < 3 && i * 4 < iv.Length; i++)
            state[12 + i] = BitConverter.ToUInt32(iv, i * 4);
        if (iv.Length >= 16) state[15] = BitConverter.ToUInt32(iv, 12);

        for (int i = 0; i < ciphertext.Length; i += 64)
        {
            Array.Copy(state, work, 16);
            for (int round = 0; round < 10; round += 2)
            {
                Quarter(work, 0, 4, 8, 12); Quarter(work, 1, 5, 9, 13);
                Quarter(work, 2, 6, 10, 14); Quarter(work, 3, 7, 11, 15);
                Quarter(work, 0, 5, 10, 15); Quarter(work, 1, 6, 11, 12);
                Quarter(work, 2, 7, 8, 13); Quarter(work, 3, 4, 9, 14);
            }
            for (int j = 0; j < 16; j++)
            {
                work[j] += state[j];
                MemoryMarshal.Write(block.AsSpan(j * 4, 4), in work[j]);
            }
            var remain = Math.Min(64, ciphertext.Length - i);
            for (int j = 0; j < remain; j++)
                output[i + j] = (byte)(ciphertext[i + j] ^ block[j]);
            state[12]++;
            if (state[12] == 0) state[13]++;
        }
        return output;
    }

    private static void Quarter(uint[] s, int a, int b, int c, int d)
    {
        s[a] += s[b]; s[d] ^= s[a]; s[d] = (s[d] << 16) | (s[d] >> 16);
        s[c] += s[d]; s[b] ^= s[c]; s[b] = (s[b] << 12) | (s[b] >> 20);
        s[a] += s[b]; s[d] ^= s[a]; s[d] = (s[d] << 8) | (s[d] >> 24);
        s[c] += s[d]; s[b] ^= s[c]; s[b] = (s[b] << 7) | (s[b] >> 25);
    }
}
