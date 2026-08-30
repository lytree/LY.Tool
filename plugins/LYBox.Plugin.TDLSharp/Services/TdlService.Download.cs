using TdLib;

namespace LYBox.Plugin.TDLSharp.Services;

public partial class TdlService
{
    const int MaxConcurrentDownloads = 5;

    public async Task DownloadFilesAsync(
        string linksText,
        string outputDir,
        string? includeExt,
        string? excludeExt,
        bool desc,
        bool group,
        bool skipSame,
        bool downloadComments,
        bool sequential,
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
            outputDir = TdlPaths.DefaultDownloadDir;
        }
        Directory.CreateDirectory(outputDir);

        var includeSet = ParseExtList(includeExt);
        var excludeSet = ParseExtList(excludeExt);

        _logger.Log($"开始下载文件，共 {links.Count} 个链接，输出目录: {outputDir}");

        // Phase 1: Collect all files to download (logging allowed)
        var filesToDownload = new List<DownloadItem>();
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
            long? albumId = null;

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
                            albumId = msg.MediaAlbumId;
                            _logger.Log($"检测到相册 (AlbumId={albumId})，共 {albumMsgs.Count} 条消息");
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

            string? linkSubFolder = albumId?.ToString();

            foreach (var msg in messagesToDownload)
            {
                ct.ThrowIfCancellationRequested();

                var subFolder = linkSubFolder;

                var file = MessageContentInspector.GetDownloadableFile(msg.Content);
                if (file == null)
                {
                    _logger.Log($"消息中无可下载的媒体: MsgId={msg.Id}");
                    continue;
                }

                var fileName = MessageContentInspector.GetFileName(msg.Content, file);
                if (!ShouldDownloadByExtension(fileName, includeSet, excludeSet))
                {
                    _logger.Log($"被扩展名过滤: {fileName}");
                    skipped++;
                    continue;
                }

                if (subFolder == null)
                {
                    subFolder = Path.GetFileNameWithoutExtension(fileName);
                    if (string.IsNullOrWhiteSpace(subFolder))
                    {
                        subFolder = $"file_{file.Id}";
                    }
                }

                var destPath = Path.Combine(outputDir, subFolder, fileName);
                if (skipSame && File.Exists(destPath))
                {
                    var existingLen = new FileInfo(destPath).Length;
                    if (existingLen == file.ExpectedSize)
                    {
                        _logger.Log($"跳过已存在文件 (同名同大小): {fileName}");
                        skipped++;
                        continue;
                    }
                }

                filesToDownload.Add(new DownloadItem
                {
                    FileId = file.Id,
                    FileName = fileName,
                    FileSize = file.ExpectedSize,
                    DestPath = destPath,
                    SubFolder = subFolder
                });
            }

            if (!downloadComments) continue;

            // Download comments if requested
            foreach (var msg in messagesToDownload)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var replyInfo = msg.InteractionInfo?.ReplyInfo;
                    if (replyInfo == null || replyInfo.ReplyCount == 0) continue;

                    var commentFolder = linkSubFolder ?? Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(MessageContentInspector.GetFileName(msg.Content, MessageContentInspector.GetDownloadableFile(msg.Content) ?? new TdApi.File { Id = 0, ExpectedSize = 0 })));
                    var commentSubFolder = $"comments_{commentFolder}";

                    long fromMsgId = 0;
                    bool hasMoreComments = true;

                    while (hasMoreComments)
                    {
                        var threadPage = await client.GetMessageThreadHistoryAsync(chatId, msg.Id, fromMsgId, 0, 100);
                        if (threadPage.Messages_ == null || threadPage.Messages_.Length == 0)
                        {
                            hasMoreComments = false;
                            break;
                        }

                        foreach (var comment in threadPage.Messages_)
                        {
                            var commentFile = MessageContentInspector.GetDownloadableFile(comment.Content);
                            if (commentFile == null) continue;
                            var commentFileName = MessageContentInspector.GetFileName(comment.Content, commentFile);
                            if (!ShouldDownloadByExtension(commentFileName, includeSet, excludeSet)) continue;

                            var commentDestPath = Path.Combine(outputDir, commentSubFolder, commentFileName);
                            if (skipSame && File.Exists(commentDestPath))
                            {
                                var existingLen = new FileInfo(commentDestPath).Length;
                                if (existingLen == commentFile.ExpectedSize)
                                {
                                    _logger.Log($"跳过评论文件 (同名同大小): {commentFileName}");
                                    skipped++;
                                    continue;
                                }
                            }

                            filesToDownload.Add(new DownloadItem
                            {
                                FileId = commentFile.Id,
                                FileName = commentFileName,
                                FileSize = commentFile.ExpectedSize,
                                DestPath = commentDestPath,
                                SubFolder = commentSubFolder
                            });
                        }

                        await Task.Delay(200, ct);
                    }
                }
                catch (TdException ex)
                {
                    _logger.Log($"获取评论失败: MsgId={msg.Id}, 错误: {ex.Error.Message}");
                }
            }
        }

        if (filesToDownload.Count == 0)
        {
            _logger.Log($"下载完成: 0 个成功, 0 个失败, {skipped} 个跳过");
            _logger.Log($"下载目录: {outputDir}");
            return;
        }

        _logger.Log(sequential
            ? $"收集到 {filesToDownload.Count} 个文件，开始同步下载"
            : $"收集到 {filesToDownload.Count} 个文件，开始并发下载 (最多 {MaxConcurrentDownloads} 个同时)");

        // Phase 2: Download
        int downloaded = 0;
        int failed = 0;

        if (sequential)
        {
            foreach (var item in filesToDownload)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await DownloadSingleFileWithProgressAsync(client, item, ct);
                    downloaded++;
                }
                catch (OperationCanceledException) { throw; }
                catch { failed++; }
            }
        }
        else
        {
            using var semaphore = new SemaphoreSlim(MaxConcurrentDownloads);
            var tasks = filesToDownload.Select(async item =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    await DownloadSingleFileWithProgressAsync(client, item, ct);
                    Interlocked.Increment(ref downloaded);
                }
                catch (OperationCanceledException) { throw; }
                catch { Interlocked.Increment(ref failed); }
                finally { semaphore.Release(); }
            }).ToList();

            await Task.WhenAll(tasks);
        }

        // Phase 3: Summary
        _logger.Log($"下载完成: {downloaded} 个成功, {failed} 个失败, {skipped} 个跳过");
        _logger.Log($"下载目录: {outputDir}");
    }

    async Task DownloadSingleFileWithProgressAsync(TdClient client, DownloadItem item, CancellationToken ct)
    {
        var progressEntry = _logger.StartProgress(item.FileName, item.FileSize, "等待中");

        try
        {
            progressEntry.StatusText = "下载中";
            var file = await client.DownloadFileAsync(item.FileId, 1, 0, 0, false);

            while (!file.Local.IsDownloadingCompleted)
            {
                ct.ThrowIfCancellationRequested();

                var downloadedSize = file.Local.DownloadedSize;
                var totalSize = file.ExpectedSize > 0 ? file.ExpectedSize : item.FileSize;
                var percentage = totalSize > 0 ? (downloadedSize * 100.0 / totalSize) : 0;

                _logger.UpdateProgress(progressEntry, percentage, $"{percentage:F1}%");

                await Task.Delay(200, ct);
                file = await client.GetFileAsync(file.Id);
            }

            if (file.Local.Path != item.DestPath && File.Exists(file.Local.Path))
            {
                var dir = Path.GetDirectoryName(item.DestPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.Copy(file.Local.Path, item.DestPath, overwrite: true);
            }

            _logger.CompleteProgress(progressEntry, "完成");
        }
        catch (OperationCanceledException)
        {
            _logger.FailProgress(progressEntry, "已取消");
            throw;
        }
        catch (Exception ex)
        {
            _logger.FailProgress(progressEntry, $"失败: {ex.Message}");
            throw;
        }
    }

    static HashSet<string> ParseExtList(string? ext)
    {
        if (string.IsNullOrWhiteSpace(ext)) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return ext.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.StartsWith('.') ? e[1..] : e)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    static bool ShouldDownloadByExtension(string fileName, HashSet<string> includeSet, HashSet<string> excludeSet)
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
}

class DownloadItem
{
    public int FileId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string DestPath { get; set; } = string.Empty;
    public string? SubFolder { get; set; }
}
