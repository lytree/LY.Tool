using Microsoft.EntityFrameworkCore;
using TdLib;

namespace Avalonia.Plugin.TDLSharp.Services;

public partial class TdlService
{
    public async Task SingleForwardAsync(string sourceLink, string targetLink,
        bool forwardComments, CancellationToken ct = default)
    {
        await EnsureReadyAsync();

        var (sourceChatId, messageId) = await ResolveSourceLinkAsync(sourceLink);
        if (sourceChatId == 0)
        {
            _logger.Log($"无法解析源链接: {sourceLink}");
            return;
        }

        if (messageId == 0)
        {
            _logger.Log($"源链接未关联到具体消息: {sourceLink}");
            return;
        }

        var targetChatId = await ResolveTargetLinkAsync(targetLink);
        if (targetChatId == 0)
        {
            _logger.Log($"无法解析目标链接: {targetLink}");
            return;
        }

        var client = Client;
        var sourceChat = await client.GetChatAsync(sourceChatId);
        var targetChat = await client.GetChatAsync(targetChatId);
        _logger.Log($"源: [{sourceChat.Title}] ChatId={sourceChatId}, MsgId={messageId}");
        _logger.Log($"目标: [{targetChat.Title}] ChatId={targetChatId}");
        _logger.Log($"评论: {(forwardComments ? "是" : "否")}");

        using var db = CreateForwardDbContext(sourceChatId);
        await db.Database.EnsureCreatedAsync();
        _logger.Log($"数据库已就绪: forward-{sourceChatId}.db");

        var message = await client.GetMessageAsync(sourceChatId, messageId);
        if (message == null)
        {
            _logger.Log($"无法获取消息: ChatId={sourceChatId}, MsgId={messageId}");
            return;
        }

        var albumMessages = await CollectAlbumMessagesAsync(client, sourceChatId, message, ct);
        _logger.Log($"收集到 {albumMessages.Count} 条消息（含同组媒体）");

        var (idsToForward, skippedIds) = await FilterAlreadyForwarded(db, sourceChatId, targetChatId, albumMessages);
        if (skippedIds.Count > 0)
        {
            _logger.Log($"跳过已转发消息 {skippedIds.Count} 条");
        }

        if (idsToForward.Count == 0)
        {
            _logger.Log("所有消息均已转发，无需重复操作");
            return;
        }

        var messagesToForward = albumMessages.Where(m => idsToForward.Contains(m.Id)).ToList();
        var groups = GroupMessagesByAlbum(messagesToForward);

        int totalForwarded = 0;

        foreach (var group in groups)
        {
            ct.ThrowIfCancellationRequested();

            int retryCount = 0;
            bool success = false;
            string? lastError = null;

            while (!success && retryCount < 5)
            {
                try
                {
                    var ids = group.Select(m => m.Id).OrderBy(id => id).ToArray();

                    var result = await client.ForwardMessagesAsync(
                        chatId: targetChatId,
                        fromChatId: sourceChatId,
                        messageIds: ids,
                        sendCopy: true,
                        removeCaption: false
                    );

                    if (result.Messages_ != null)
                    {
                        foreach (var rMsg in result.Messages_)
                        {
                            RegisterPendingSend(rMsg.Id);
                        }
                    }

                    var sendError = await WaitForSendResultAsync(
                        result.Messages_?.Select(m => m.Id).ToArray() ?? [], 10);

                    if (sendError != null)
                    {
                        if (sendError.Code == 429 || (sendError.Message?.Contains("Too Many Requests") ?? false))
                        {
                            int retryAfter = ParseRetryAfterFromError(sendError);
                            retryCount++;
                            _logger.Log($"异步发送触发频率限制 (第{retryCount}次)，等待 {retryAfter} 秒后重试...");
                            await Task.Delay(retryAfter * 1000, ct);
                            continue;
                        }

                        lastError = $"{sendError.Code}: {sendError.Message}";
                        retryCount++;
                        _logger.Log($"消息异步发送失败 (第{retryCount}次重试): {lastError}");
                        await Task.Delay(5000, ct);
                        continue;
                    }

                    await Task.Delay(1000, ct);
                    var forwardedMessages = group.Where(m => idsToForward.Contains(m.Id)).ToList();
                    await RecordForwardedMessages(db, sourceChatId, targetChatId, forwardedMessages, isSuccess: true, result.Messages_);

                    totalForwarded += ids.Length;
                    var albumLabel = group.First().MediaAlbumId != 0
                        ? $"分组:{group.First().MediaAlbumId}"
                        : $"独立消息 {group.First().Id}";
                    _logger.Log($"已转发 ({albumLabel}, 数量: {ids.Length})");

                    if (forwardComments && result.Messages_ != null)
                    {
                        await ForwardCommentsForMessages(db, sourceChatId, targetChatId, forwardedMessages, result.Messages_, ct);
                    }

                    success = true;
                }
                catch (TdException ex) when (ex.Error.Code == 429)
                {
                    int retryAfter = ParseRetryAfter(ex);
                    retryCount++;
                    _logger.Log($"触发频率限制 (第{retryCount}次)，等待 {retryAfter} 秒后重试...");
                    await Task.Delay(retryAfter * 1000, ct);
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                    retryCount++;
                    _logger.Log($"转发消息时出错 (第{retryCount}次重试): {ex.Message}");
                    await Task.Delay(5000, ct);
                }
            }

            if (!success)
            {
                var failedMessages = group.Where(m => idsToForward.Contains(m.Id)).ToList();
                await RecordForwardedMessages(db, sourceChatId, targetChatId, failedMessages, isSuccess: false, error: lastError);
                _logger.Log($"消息转发失败 (MediaAlbumId: {group.First().MediaAlbumId})");
            }
        }

        _logger.Log($"单条深度转发完成，共转发 {totalForwarded} 条消息");
    }

    async Task<List<TdApi.Message>> CollectAlbumMessagesAsync(TdClient client, long chatId, TdApi.Message seedMessage, CancellationToken ct)
    {
        var result = new List<TdApi.Message> { seedMessage };

        if (seedMessage.MediaAlbumId == 0) return result;

        long albumId = seedMessage.MediaAlbumId;
        long seedId = seedMessage.Id;
        bool hasMore = true;
        long fromMessageId = 0;

        while (hasMore)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var history = await client.GetChatHistoryAsync(chatId, fromMessageId, 0, 100, false);
                if (history.Messages_ == null || history.Messages_.Length == 0)
                {
                    hasMore = false;
                    break;
                }

                foreach (var msg in history.Messages_)
                {
                    if (msg.MediaAlbumId == albumId && msg.Id != seedId)
                    {
                        result.Add(msg);
                    }
                }

                var foundSeed = history.Messages_.Any(m => m.Id >= seedId);
                if (foundSeed)
                {
                    hasMore = false;
                    break;
                }

                fromMessageId = history.Messages_.Last().Id;
                await Task.Delay(300, ct);
            }
            catch (Exception ex)
            {
                _logger.Log($"收集同组媒体消息时异常: {ex.Message}");
                hasMore = false;
            }
        }

        return result.OrderBy(m => m.Id).ToList();
    }
}
