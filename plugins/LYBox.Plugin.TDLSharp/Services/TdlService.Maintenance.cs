using TdLib;

namespace LYBox.Plugin.TDLSharp.Services;

public partial class TdlService
{
    public async Task ClearMessagesAsync(string? channelLink, string containsText, bool silent, int limit, CancellationToken ct = default)
    {
        await EnsureReadyAsync();

        var client = Client;
        var currentUser = await GetCurrentUserAsync();
        long myId = currentUser.Id;

        long chatId = await ResolveChatIdAsync(channelLink);
        if (chatId == 0)
        {
            chatId = myId;
            _logger.Log($"未指定频道，默认使用收藏夹 (ChatId={myId})");
        }

        var chat = await client.GetChatAsync(chatId);
        _logger.Log($"目标: [{chat.Title}] ChatId={chatId}");
        _logger.Log($"匹配内容: \"{containsText}\"");
        _logger.Log($"删除模式: {(silent ? "静默删除" : "交互确认")}");

        int totalDeleted = await CleanMessages(client, chatId, containsText, silent, limit, ct);
        _logger.Log($"清理完成，共删除 {totalDeleted} 条消息");
    }

    async Task<int> CleanMessages(TdClient client, long chatId, string containsText, bool silent, int limit, CancellationToken ct)
    {
        int totalDeleted = 0;
        long fromMessageId = 0;
        bool hasMore = true;
        var matchedMessages = new List<(long MsgId, string Text)>();

        _logger.Log("开始扫描消息...");

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
                    var text = MessageContentInspector.GetText(msg.Content);
                    if (text != null && text.Contains(containsText, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedMessages.Add((msg.Id, text.Length > 80 ? text[..80] + "..." : text));
                    }

                    if (limit > 0 && matchedMessages.Count >= limit)
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

        if (matchedMessages.Count == 0)
        {
            _logger.Log($"未找到包含 \"{containsText}\" 的消息");
            return 0;
        }

        _logger.Log($"共找到 {matchedMessages.Count} 条匹配消息");

        const int batchSize = 100;
        for (int i = 0; i < matchedMessages.Count; i += batchSize)
        {
            ct.ThrowIfCancellationRequested();
            var batch = matchedMessages.Skip(i).Take(batchSize).Select(m => m.MsgId).ToArray();
            try
            {
                await client.DeleteMessagesAsync(chatId, batch, revoke: true);
                totalDeleted += batch.Length;
                _logger.Log($"已删除 {totalDeleted}/{matchedMessages.Count} 条消息");
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

        return totalDeleted;
    }
}
