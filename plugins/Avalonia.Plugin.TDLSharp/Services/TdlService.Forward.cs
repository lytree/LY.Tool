using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using TdLib;

namespace Avalonia.Plugin.TDLSharp.Services;

public partial class TdlService
{
    public async Task BatchForwardAsync(string sourceLink, string? sourceId, string targetLink,
        bool older, int limit, bool forwardComments, CancellationToken ct = default)
    {
        await EnsureReadyAsync();

        var (sourceChatId, startMessageId) = await ResolveSourceLinkAsync(sourceLink);
        if (sourceChatId == 0)
        {
            _logger.Log($"无法解析源链接: {sourceLink}");
            return;
        }

        if (!string.IsNullOrEmpty(sourceId) && long.TryParse(sourceId, out var sid))
        {
            startMessageId = sid;
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
        _logger.Log($"源: [{sourceChat.Title}] ChatId={sourceChatId}, StartMsgId={startMessageId}");
        _logger.Log($"目标: [{targetChat.Title}] ChatId={targetChatId}");
        _logger.Log($"方向: {(older ? "向旧消息" : "向新消息")}, 限制: {(limit > 0 ? limit.ToString() : "无限制")}, 评论: {(forwardComments ? "是" : "否")}");

        using var db = CreateForwardDbContext(sourceChatId);
        await db.Database.EnsureCreatedAsync();
        _logger.Log($"数据库已就绪: forward-{sourceChatId}.db");

        int totalForwarded;
        if (older)
        {
            totalForwarded = await ForwardOlderDirection(db, sourceChatId, startMessageId, targetChatId, limit, forwardComments, ct);
        }
        else
        {
            totalForwarded = await ForwardNewerDirection(db, sourceChatId, startMessageId, targetChatId, limit, forwardComments, ct);
        }

        _logger.Log($"全部转发完成，共转发 {totalForwarded} 条消息");
    }

    public async Task<int> DeepCopyForward(ForwardDbContext db, long sourceChatId, long startMessageId, long targetChatId, int limit, bool forwardComments, CancellationToken ct = default)
    {
        int totalForwarded = 0;
        int totalSkipped = 0;
        long fromMessageId = startMessageId;
        List<TdApi.Message>? pendingGroup = null;
        bool hasMore = true;

        _logger.Log("开始向旧消息方向转发...");

        while (hasMore)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var history = await Client.GetChatHistoryAsync(sourceChatId, fromMessageId, 0, 100, false);
                if (history.Messages_ == null || history.Messages_.Length == 0)
                {
                    hasMore = false;
                    break;
                }

                var messages = history.Messages_
                    .Where(m => m.Id <= fromMessageId && m.ForwardInfo != null)
                    .OrderBy(m => m.Id)
                    .ToList();

                if (messages.Count == 0)
                {
                    fromMessageId = history.Messages_.Last().Id;
                    continue;
                }

                if (pendingGroup != null && pendingGroup.Count > 0)
                {
                    messages = [.. pendingGroup, .. messages];
                    pendingGroup = null;
                }

                var (toProcess, pending) = ExtractPendingMediaGroup(messages);
                if (pending != null && pending.Count > 0)
                {
                    pendingGroup = pending;
                }

                var (forwarded, skipped) = await ForwardGroupedMessages(db, toProcess, sourceChatId, targetChatId, forwardComments, ct);
                totalForwarded += forwarded;
                totalSkipped += skipped;

                if (limit > 0 && totalForwarded >= limit)
                {
                    _logger.Log($"已达到转发限制 {limit}");
                    break;
                }

                fromMessageId = history.Messages_.Last().Id;
                await Task.Delay(1000, ct);
            }
            catch (TdException ex) when (ex.Error.Code == 429)
            {
                int retryAfter = ParseRetryAfter(ex);
                _logger.Log($"触发频率限制，等待 {retryAfter} 秒后继续...");
                await Task.Delay(retryAfter * 1000, ct);
            }
            catch (Exception ex)
            {
                _logger.Log($"转发过程中发生异常: {ex.Message}");
                await Task.Delay(5000, ct);
            }
        }

        if (pendingGroup != null && pendingGroup.Count > 0)
        {
            var (forwarded, skipped) = await ForwardGroupedMessages(db, pendingGroup, sourceChatId, targetChatId, forwardComments, ct);
            totalForwarded += forwarded;
            totalSkipped += skipped;
        }

        if (totalSkipped > 0)
        {
            _logger.Log($"跳过已转发消息 {totalSkipped} 条");
        }

        return totalForwarded;
    }

    public async Task<int> ForwardOlderDirection(ForwardDbContext db, long sourceChatId, long startMessageId, long targetChatId, int limit, bool forwardComments, CancellationToken ct = default)
    {
        int totalForwarded = 0;
        int totalSkipped = 0;
        long fromMessageId = startMessageId;
        List<TdApi.Message>? pendingGroup = null;
        bool hasMore = true;

        _logger.Log("开始向旧消息方向转发...");

        while (hasMore)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var history = await Client.GetChatHistoryAsync(sourceChatId, fromMessageId, 0, 100, false);
                if (history.Messages_ == null || history.Messages_.Length == 0)
                {
                    hasMore = false;
                    break;
                }

                var messages = history.Messages_
                    .Where(m => m.Id <= fromMessageId)
                    .OrderBy(m => m.Id)
                    .ToList();

                if (messages.Count == 0)
                {
                    fromMessageId = history.Messages_.Last().Id;
                    continue;
                }

                if (pendingGroup != null && pendingGroup.Count > 0)
                {
                    messages = [.. pendingGroup, .. messages];
                    pendingGroup = null;
                }

                var (toProcess, pending) = ExtractPendingMediaGroup(messages);
                if (pending != null && pending.Count > 0)
                {
                    pendingGroup = pending;
                }

                var (forwarded, skipped) = await ForwardGroupedMessages(db, toProcess, sourceChatId, targetChatId, forwardComments, ct);
                totalForwarded += forwarded;
                totalSkipped += skipped;

                if (limit > 0 && totalForwarded >= limit)
                {
                    _logger.Log($"已达到转发限制 {limit}");
                    break;
                }

                fromMessageId = history.Messages_.Last().Id;
                await Task.Delay(1000, ct);
            }
            catch (TdException ex) when (ex.Error.Code == 429)
            {
                int retryAfter = ParseRetryAfter(ex);
                _logger.Log($"触发频率限制，等待 {retryAfter} 秒后继续...");
                await Task.Delay(retryAfter * 1000, ct);
            }
            catch (Exception ex)
            {
                _logger.Log($"转发过程中发生异常: {ex.Message}");
                await Task.Delay(5000, ct);
            }
        }

        if (pendingGroup != null && pendingGroup.Count > 0)
        {
            var (forwarded, skipped) = await ForwardGroupedMessages(db, pendingGroup, sourceChatId, targetChatId, forwardComments, ct);
            totalForwarded += forwarded;
            totalSkipped += skipped;
        }

        if (totalSkipped > 0)
        {
            _logger.Log($"跳过已转发消息 {totalSkipped} 条");
        }

        return totalForwarded;
    }

    public async Task<int> ForwardNewerDirection(ForwardDbContext db, long sourceChatId, long startMessageId, long targetChatId, int limit, bool forwardComments, CancellationToken ct = default)
    {
        var newerMessages = new List<TdApi.Message>();
        long fromMessageId = 0;
        bool foundStart = false;

        _logger.Log("开始向新消息方向转发（从最新消息往回搜索）...");

        while (!foundStart)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var history = await Client.GetChatHistoryAsync(sourceChatId, fromMessageId, 0, 100, false);
                if (history.Messages_ == null || history.Messages_.Length == 0)
                {
                    break;
                }

                foreach (var msg in history.Messages_)
                {
                    if (msg.Id >= startMessageId)
                    {
                        newerMessages.Add(msg);
                        if (limit > 0 && newerMessages.Count >= limit)
                        {
                            foundStart = true;
                            break;
                        }
                    }
                    else
                    {
                        foundStart = true;
                        break;
                    }
                }

                fromMessageId = history.Messages_.Last().Id;
                await Task.Delay(500, ct);
            }
            catch (Exception ex)
            {
                _logger.Log($"搜索新消息时发生异常: {ex.Message}");
                break;
            }
        }

        newerMessages = newerMessages.OrderBy(m => m.Id).ToList();
        _logger.Log($"找到 {newerMessages.Count} 条消息，开始转发...");

        var (totalForwarded, totalSkipped) = await ForwardGroupedMessages(db, newerMessages, sourceChatId, targetChatId, forwardComments, ct);

        if (totalSkipped > 0)
        {
            _logger.Log($"跳过已转发消息 {totalSkipped} 条");
        }

        return totalForwarded;
    }

    async Task<(int forwarded, int skipped)> ForwardGroupedMessages(ForwardDbContext db, List<TdApi.Message> messages, long sourceChatId, long targetChatId, bool forwardComments, CancellationToken ct = default)
    {
        if (messages.Count == 0) return (0, 0);

        int totalForwarded = 0;
        int totalSkipped = 0;
        var groups = GroupMessagesByAlbum(messages);

        foreach (var group in groups)
        {
            ct.ThrowIfCancellationRequested();

            var (idsToForward, skippedIds) = await FilterAlreadyForwarded(db, sourceChatId, targetChatId, group);
            totalSkipped += skippedIds.Count;

            if (idsToForward.Count == 0)
            {
                continue;
            }

            int retryCount = 0;
            bool success = false;
            string? lastError = null;

            while (!success && retryCount < 5)
            {
                try
                {
                    var ids = idsToForward.OrderBy(id => id).ToArray();

                    var result = await Client.ForwardMessagesAsync(
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
                    var albumLabel = group.First().MediaAlbumId != 0 ? $"分组:{group.First().MediaAlbumId}" : $"独立消息 {group.First().Id}";
                    _logger.Log($"已转发  ({albumLabel}, 数量: {ids.Length})");

                    if (forwardComments && result.Messages_ != null)
                    {
                        await ForwardCommentsForMessages(db, sourceChatId, targetChatId, forwardedMessages, result.Messages_, ct);
                    }

                    await Task.Delay(1000, ct);
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
                    _logger.Log($"转发消息组时出错 (第{retryCount}次重试): {ex.Message}");
                    await Task.Delay(5000, ct);
                }
            }

            if (!success)
            {
                var failedMessages = group.Where(m => idsToForward.Contains(m.Id)).ToList();
                await RecordForwardedMessages(db, sourceChatId, targetChatId, failedMessages, isSuccess: false, error: lastError);
                _logger.Log($"消息组转发失败，已跳过 (MediaAlbumId: {group.First().MediaAlbumId})");
            }
        }

        return (totalForwarded, totalSkipped);
    }

    async Task ForwardCommentsForMessages(ForwardDbContext db, long sourceChatId, long targetChatId, List<TdApi.Message> sourceMessages, TdApi.Message[] forwardedMessages, CancellationToken ct = default)
    {
        for (int i = 0; i < sourceMessages.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var sourceMsg = sourceMessages[i];
            TdApi.Message? forwardedMsg = i < forwardedMessages.Length ? forwardedMessages[i] : null;

            if (forwardedMsg == null) continue;
            try
            {
                var replyInfo = sourceMsg.InteractionInfo?.ReplyInfo;
                if (replyInfo == null || replyInfo.ReplyCount == 0) continue;

                var allComments = new List<TdApi.Message>();
                long fromMsgId = 0;
                bool hasMore = true;

                while (hasMore)
                {
                    var page = await Client.GetMessageThreadHistoryAsync(
                        chatId: sourceChatId,
                        messageId: sourceMsg.Id,
                        fromMessageId: fromMsgId,
                        offset: 0,
                        limit: 100
                    );

                    if (page.Messages_ == null || page.Messages_.Length == 0)
                    {
                        hasMore = false;
                        break;
                    }

                    allComments.AddRange(page.Messages_);

                    if (page.Messages_.Length < 100)
                    {
                        hasMore = false;
                    }
                    else
                    {
                        fromMsgId = page.Messages_.Min(m => m.Id);
                        await Task.Delay(300, ct);
                    }
                }
                if (allComments.Count == 0) continue;

                var commentList = allComments.OrderBy(m => m.Id).ToList();
                var groups = GroupMessagesByAlbum(commentList);
                _logger.Log($"转发评论: MsgId={sourceMsg.Id}, 评论数={commentList.Count}, 分组数={groups.Count}");

                foreach (var group in groups)
                {
                    var (idsToForward, skippedIds) = await FilterAlreadyForwarded(db, sourceChatId, targetChatId, group);

                    if (idsToForward.Count == 0) continue;

                    var groupIds = group.Select(m => m.Id).OrderBy(id => id).ToArray();
                    var sourceCommonChatId = group.Select(m => m.ChatId).OrderBy(id => id).First();
                    var albumLabel = group[0].MediaAlbumId != 0 ? $"分组:{group[0].MediaAlbumId}" : $"独立评论 {group[0].Id}";

                    var result = await Client.ForwardMessagesAsync(
                         chatId: targetChatId,
                         fromChatId: sourceCommonChatId,
                         messageIds: groupIds,
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
                            _logger.Log($"异步发送触发频率限制，等待 {retryAfter} 秒后重试...");
                            await Task.Delay(retryAfter * 1000, ct);
                            continue;
                        }

                        _logger.Log($"消息异步发送失败: {sendError.Code}: {sendError.Message}");
                        await Task.Delay(5000, ct);
                        continue;
                    }
                    await Task.Delay(1000, ct);
                    var forwardedCommentsMessages = group.Where(m => groupIds.Contains(m.Id)).ToList();
                    await RecordForwardedMessages(db, sourceChatId, targetChatId, forwardedCommentsMessages, isSuccess: true);
                    _logger.Log($"已转发评论 {albumLabel}, 数量: {groupIds.Length}");
                    await Task.Delay(5000, ct);
                }
            }
            catch (TdException ex)
            {
                _logger.Log($"转发评论失败: MsgId={sourceMsg.Id}, 错误: {ex.Error.Message}");
            }
            catch (Exception ex)
            {
                _logger.Log($"转发评论异常: MsgId={sourceMsg.Id}, 错误: {ex.Message}");
            }
        }
    }

    async Task<(List<long> idsToForward, List<long> skippedIds)> FilterAlreadyForwarded(ForwardDbContext db, long sourceChatId, long targetChatId, List<TdApi.Message> messages)
    {
        var messageIds = messages.Select(m => m.Id).ToHashSet();

        var alreadyForwarded = await db.ForwardRecords
            .Where(r => r.SourceChatId == sourceChatId && r.TargetChatId == targetChatId && messageIds.Contains(r.MessageId))
            .Select(r => r.MessageId)
            .ToHashSetAsync();

        var idsToForward = messageIds.Except(alreadyForwarded).ToList();
        return (idsToForward, alreadyForwarded.ToList());
    }

    async Task RecordForwardedMessages(ForwardDbContext db, long sourceChatId, long targetChatId, List<TdApi.Message> messages, bool isSuccess, TdApi.Message[]? forwardedMessages = null, string? error = null)
    {
        foreach (var msg in messages)
        {
            var record = new ForwardRecord
            {
                SourceChatId = sourceChatId,
                TargetChatId = targetChatId,
                MessageId = msg.Id,
                MediaAlbumId = msg.MediaAlbumId,
                IsSuccess = isSuccess,
                ForwardedAt = DateTime.UtcNow
            };

            if (isSuccess && forwardedMessages != null)
            {
                var fwdMsg = forwardedMessages.FirstOrDefault(m => m.Id > 0);
                if (fwdMsg != null)
                {
                    record.NewMessageId = fwdMsg.Id;
                }
            }

            db.ForwardRecords.Add(record);
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (Exception ex) { Debug.WriteLine($"[TdlService] 保存转发记录失败: {ex.Message}"); }
    }
}
