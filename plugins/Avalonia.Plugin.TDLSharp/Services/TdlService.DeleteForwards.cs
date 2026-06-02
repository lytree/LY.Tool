using TdLib;

namespace Avalonia.Plugin.TDLSharp.Services;

public partial class TdlService
{
    public async Task DeleteAllForwardMessagesAsync(string? channelLink, string? fromLink, int limit, CancellationToken ct = default)
    {
        await EnsureReadyAsync();

        var client = Client;
        var currentUser = await GetCurrentUserAsync();
        long myId = currentUser.Id;

        long chatId;
        long startMessageId = 0;

        if (!string.IsNullOrWhiteSpace(fromLink))
        {
            var (resolvedChatId, resolvedMsgId) = await ResolveSourceLinkAsync(fromLink);
            if (resolvedChatId == 0 || resolvedMsgId == 0)
            {
                _logger.Log($"无法解析起始链接: {fromLink}");
                return;
            }

            chatId = resolvedChatId;
            startMessageId = resolvedMsgId;
            _logger.Log($"起始消息: ChatId={chatId}, MessageId={startMessageId}");
        }
        else
        {
            chatId = await ResolveChatIdAsync(channelLink);
            if (chatId == 0)
            {
                chatId = myId;
                _logger.Log($"未指定频道，默认使用收藏夹 (ChatId={myId})");
            }
        }

        var chat = await client.GetChatAsync(chatId);
        _logger.Log($"目标: [{chat.Title}] ChatId={chatId}");

        if (startMessageId != 0)
        {
            _logger.Log($"仅删除消息 {startMessageId} 之前的转发消息");
        }

        var forwardedMessageIds = new List<long>();
        long fromMessageId = startMessageId;
        bool hasMore = true;

        _logger.Log("开始扫描转发消息...");

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
                    if (msg.ForwardInfo != null)
                    {
                        forwardedMessageIds.Add(msg.Id);
                    }

                    if (limit > 0 && forwardedMessageIds.Count >= limit)
                    {
                        hasMore = false;
                        break;
                    }
                }

                fromMessageId = history.Messages_.Last().Id;
                await Task.Delay(300, ct);
            }
            catch (TdException ex) when (ex.Error.Code == 429)
            {
                int retryAfter = ParseRetryAfter(ex);
                _logger.Log($"触发频率限制，等待 {retryAfter} 秒后继续...");
                await Task.Delay(retryAfter * 1000, ct);
            }
            catch (Exception ex)
            {
                _logger.Log($"扫描消息时发生异常: {ex.Message}");
                await Task.Delay(5000, ct);
            }
        }

        if (forwardedMessageIds.Count == 0)
        {
            _logger.Log("未找到转发消息");
            return;
        }

        _logger.Log($"共找到 {forwardedMessageIds.Count} 条转发消息，开始删除...");

        int totalDeleted = 0;
        int batchSize = 100;

        for (int i = 0; i < forwardedMessageIds.Count; i += batchSize)
        {
            ct.ThrowIfCancellationRequested();

            var batch = forwardedMessageIds.Skip(i).Take(batchSize).ToArray();
            try
            {
                await client.DeleteMessagesAsync(chatId, batch, revoke: true);
                totalDeleted += batch.Length;
                _logger.Log($"已删除 {totalDeleted}/{forwardedMessageIds.Count} 条转发消息");
                await Task.Delay(500, ct);
            }
            catch (TdException ex) when (ex.Error.Code == 429)
            {
                int retryAfter = ParseRetryAfter(ex);
                _logger.Log($"触发频率限制，等待 {retryAfter} 秒后继续...");
                await Task.Delay(retryAfter * 1000, ct);
                i -= batchSize;
            }
            catch (Exception ex)
            {
                _logger.Log($"批量删除消息时发生异常: {ex.Message}");
            }
        }

        _logger.Log($"删除完成，共删除 {totalDeleted} 条转发消息");
    }
}
