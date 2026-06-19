using TdLib;

namespace Avalonia.Plugin.TDLSharp.Services;

public partial class TdlService
{
    public async Task UploadFilesAsync(
        string pathsText,
        string? chatLink,
        bool asPhoto,
        bool autoDelete,
        CancellationToken ct = default)
    {
        await EnsureReadyAsync();

        var client = Client;

        var paths = pathsText.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        var files = new List<string>();
        foreach (var p in paths)
        {
            if (Directory.Exists(p))
            {
                files.AddRange(Directory.GetFiles(p, "*", SearchOption.TopDirectoryOnly));
            }
            else if (File.Exists(p))
            {
                files.Add(p);
            }
        }

        if (files.Count == 0)
        {
            _logger.Log("没有可上传的有效文件");
            return;
        }

        long targetChatId = 0;
        if (!string.IsNullOrWhiteSpace(chatLink))
        {
            targetChatId = await ResolveTargetLinkAsync(chatLink);
            if (targetChatId == 0)
            {
                _logger.Log($"无法解析目标聊天: {chatLink}");
                return;
            }
        }
        else
        {
            var me = await GetCurrentUserAsync();
            targetChatId = me.Id;
            _logger.Log($"未指定聊天，默认使用收藏夹 (ChatId={targetChatId})");
        }

        var chat = await client.GetChatAsync(targetChatId);
        _logger.Log($"目标: [{chat.Title}] ChatId={targetChatId}");
        _logger.Log($"开始上传文件，共 {files.Count} 个文件");

        int uploaded = 0;
        foreach (var filePath in files)
        {
            ct.ThrowIfCancellationRequested();

            var fileName = Path.GetFileName(filePath);
            var fileInfo = new FileInfo(filePath);
            _logger.Log($"正在上传: {fileName} ({fileInfo.Length} 字节)");

            try
            {
                await UploadSingleFileAsync(client, targetChatId, filePath, asPhoto, ct);
                uploaded++;

                if (autoDelete)
                {
                    try
                    {
                        File.Delete(filePath);
                        _logger.Log($"已上传并删除: {fileName}");
                    }
                    catch (Exception ex)
                    {
                        _logger.Log($"删除文件失败: {fileName} - {ex.Message}");
                    }
                }
            }
            catch (TdException ex)
            {
                _logger.Log($"上传失败: {fileName} - {ex.Error.Message}");
            }
        }

        _logger.Log($"上传完成: {uploaded} 个文件");
    }

    async Task UploadSingleFileAsync(TdClient client, long chatId, string filePath, bool asPhoto, CancellationToken ct)
    {
        var fileName = Path.GetFileName(filePath);

        TdApi.InputFile inputFile = new TdApi.InputFile.InputFileLocal
        {
            Path = filePath
        };

        TdApi.InputMessageContent content;

        if (asPhoto && IsImageFile(fileName))
        {
            content = new TdApi.InputMessageContent.InputMessagePhoto
            {
                Photo = inputFile,
                Width = 0,
                Height = 0,
                Caption = null
            };
        }
        else
        {
            content = new TdApi.InputMessageContent.InputMessageDocument
            {
                Document = inputFile,
                Thumbnail = null,
                DisableContentTypeDetection = false,
                Caption = null
            };
        }

        await client.SendMessageAsync(
            chatId: chatId,
            replyTo: null,
            options: null,
            replyMarkup: null,
            inputMessageContent: content);
    }

    bool IsImageFile(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp" or ".gif";
    }
}
