using System.Text.Json;
using TdLib;

namespace Avalonia.Plugin.TDLSharp.Services;

public partial class TdlService
{
    public async Task DownloadFilesAsync(
        string linksText,
        string outputDir,
        string? includeExt,
        string? excludeExt,
        bool desc,
        bool group,
        bool skipSame,
        CancellationToken ct = default)
    {
        await EnsureReadyAsync();

        var client = Client;

        var links = linksText.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        if (links.Count == 0)
        {
            _logger.Log("没有提供消息链接");
            return;
        }

        if (string.IsNullOrWhiteSpace(outputDir))
        {
            outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "tdl", "download");
        }
        Directory.CreateDirectory(outputDir);

        var includeSet = ParseExtList(includeExt);
        var excludeSet = ParseExtList(excludeExt);

        _logger.Log($"开始下载文件，共 {links.Count} 个链接，输出目录: {outputDir}");

        int downloaded = 0;
        int skipped = 0;

        foreach (var link in links)
        {
            ct.ThrowIfCancellationRequested();

            var (chatId, messageId) = await ResolveSourceLinkAsync(link);
            if (chatId == 0)
            {
                _logger.Log($"无法解析链接: {link}");
                continue;
            }

            var messagesToDownload = new List<TdApi.Message>();

            try
            {
                var msg = await client.GetMessageAsync(chatId, messageId);
                messagesToDownload.Add(msg);

                if (group && msg.MediaAlbumId != 0)
                {
                    var history = await client.GetChatHistoryAsync(chatId, messageId, 0, 20, false);
                    if (history.Messages_ != null)
                    {
                        var albumMsgs = history.Messages_
                            .Where(m => m.MediaAlbumId == msg.MediaAlbumId)
                            .OrderBy(m => m.Id)
                            .ToList();
                        if (albumMsgs.Count > 1)
                        {
                            messagesToDownload = albumMsgs;
                            _logger.Log($"检测到相册，共 {albumMsgs.Count} 条消息");
                        }
                    }
                }
            }
            catch (TdException ex)
            {
                _logger.Log($"获取消息失败: {link} - {ex.Error.Message}");
                continue;
            }

            if (desc)
            {
                messagesToDownload.Reverse();
            }

            foreach (var msg in messagesToDownload)
            {
                ct.ThrowIfCancellationRequested();

                var file = ExtractDownloadableFile(msg.Content);
                if (file == null)
                {
                    _logger.Log($"消息中无可下载的媒体: MsgId={msg.Id}");
                    continue;
                }

                var fileName = GetFileName(msg, file);
                if (!ShouldDownloadByExtension(fileName, includeSet, excludeSet))
                {
                    _logger.Log($"被扩展名过滤: {fileName}");
                    skipped++;
                    continue;
                }

                var destPath = Path.Combine(outputDir, fileName);
                if (skipSame && File.Exists(destPath))
                {
                    var existingLen = new FileInfo(destPath).Length;
                    if (existingLen == file.ExpectedSize)
                    {
                        _logger.Log($"跳过 (同名同大小): {fileName}");
                        skipped++;
                        continue;
                    }
                }

                try
                {
                    _logger.Log($"正在下载: {fileName} ({file.ExpectedSize} 字节)");
                    var downloadedFile = await DownloadFileAsync(client, file.Id, destPath, ct);
                    downloaded++;
                    _logger.Log($"下载完成: {downloadedFile.Local.Path}");
                }
                catch (TdException ex)
                {
                    _logger.Log($"下载失败: {fileName} - {ex.Error.Message}");
                }
            }
        }

        _logger.Log($"下载完成: {downloaded} 个文件, 跳过 {skipped} 个");
    }

    TdApi.File? ExtractDownloadableFile(TdApi.MessageContent content)
    {
        return content switch
        {
            TdApi.MessageContent.MessagePhoto p => p.Photo.Sizes.LastOrDefault()?.Photo,
            TdApi.MessageContent.MessageVideo v => v.Video.Video_,
            TdApi.MessageContent.MessageAudio a => a.Audio.Audio_,
            TdApi.MessageContent.MessageDocument d => d.Document.Document_,
            TdApi.MessageContent.MessageVoiceNote vn => vn.VoiceNote.Voice,
            TdApi.MessageContent.MessageVideoNote vn => vn.VideoNote.Video,
            TdApi.MessageContent.MessageAnimation ani => ani.Animation.Animation_,
            TdApi.MessageContent.MessageSticker s => s.Sticker.Sticker_,
            _ => null
        };
    }

    string GetFileName(TdApi.Message msg, TdApi.File file)
    {
        var name = msg.Content switch
        {
            TdApi.MessageContent.MessageVideo v => v.Video.FileName,
            TdApi.MessageContent.MessageAudio a => a.Audio.FileName,
            TdApi.MessageContent.MessageDocument d => d.Document.FileName,
            TdApi.MessageContent.MessageAnimation ani => ani.Animation.FileName,
            TdApi.MessageContent.MessageSticker s => $"{s.Sticker.SetId}_{s.Sticker.Sticker_.Id}.webp",
            _ => $"file_{file.Id}"
        };

        if (string.IsNullOrWhiteSpace(name))
        {
            name = $"file_{file.Id}";
        }

        return name;
    }

    HashSet<string> ParseExtList(string? ext)
    {
        if (string.IsNullOrWhiteSpace(ext)) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return ext.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.StartsWith('.') ? e[1..] : e)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    bool ShouldDownloadByExtension(string fileName, HashSet<string> includeSet, HashSet<string> excludeSet)
    {
        var ext = Path.GetExtension(fileName).TrimStart('.');

        if (includeSet.Count > 0)
        {
            return includeSet.Contains(ext);
        }

        if (excludeSet.Count > 0)
        {
            return !excludeSet.Contains(ext);
        }

        return true;
    }

    async Task<TdApi.File> DownloadFileAsync(TdClient client, int fileId, string destPath, CancellationToken ct)
    {
        var file = await client.DownloadFileAsync(fileId, 1, 0, 0, false);

        while (!file.Local.IsDownloadingCompleted)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(200, ct);
            file = await client.GetFileAsync(file.Id);
        }

        if (file.Local.Path != destPath && File.Exists(file.Local.Path))
        {
            var dir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.Copy(file.Local.Path, destPath, overwrite: true);
        }

        return file;
    }
}
