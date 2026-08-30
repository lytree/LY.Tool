using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using TdLib;

namespace LYBox.Plugin.TDLSharp.Services;

public partial class TdlService
{
    /// <summary>
    /// 批量转发多个源到同一目标。支持多行源链接输入（每行一个），可选 "link|sourceId" 格式指定起始消息ID。
    /// 三种目标主题模式（按优先级）：
    /// 1. fixedTopicName 非空：所有源转发到该固定话题（目标须为论坛超级群组，话题不存在时自动创建）。
    /// 2. classifyBySource=true：按源频道名称创建/复用主题，每个源对应一个主题。
    /// 3. 两者均未启用：所有源转发到目标主聊天（messageThreadId=0）。
    /// tags 用于在每组转发消息后追加一条独立标签消息；为空时默认使用源链接提取的标签。
    /// </summary>
    public async Task BatchForwardClassifiedAsync(
        string sourcesMultiLine,
        string? fallbackSourceId,
        string targetLink,
        string? fixedTopicName,
        bool older,
        int limit,
        bool forwardComments,
        bool classifyBySource,
        string? tags,
        CancellationToken ct = default)
    {
        await EnsureReadyAsync();

        var sources = ParseMultiLineSources(sourcesMultiLine);
        if (sources.Count == 0)
        {
            _logger.Log("未输入任何源链接");
            return;
        }

        var targetChatId = await ResolveTargetLinkAsync(targetLink);
        if (targetChatId == 0)
        {
            _logger.Log($"无法解析目标链接: {targetLink}");
            return;
        }

        var client = Client;
        var targetChat = await client.GetChatAsync(targetChatId);
        _logger.Log($"目标: [{targetChat.Title}] ChatId={targetChatId}");

        bool hasFixedTopic = !string.IsNullOrWhiteSpace(fixedTopicName);
        bool needsForum = hasFixedTopic || classifyBySource;

        // 固定话题/分类模式下校验目标是否为论坛
        long fixedTopicId = 0;
        bool isForum = false;
        if (needsForum)
        {
            isForum = await IsForumChatAsync(targetChatId);
            if (!isForum)
            {
                _logger.Log($"目标 [{targetChat.Title}] 不是论坛超级群组，无法使用话题功能。请在 Telegram 中将目标群组开启「话题」功能后重试。");
                return;
            }

            if (hasFixedTopic)
            {
                // 固定话题模式：一次性解析话题名称为 ID，所有源共用
                fixedTopicId = await CreateOrFindForumTopicAsync(targetChatId, fixedTopicName!);
                if (fixedTopicId == 0)
                {
                    _logger.Log($"无法解析或创建固定话题 [{fixedTopicName}]，终止转发");
                    return;
                }
                _logger.Log($"已启用固定话题模式，所有源将转发到话题 [{fixedTopicName}] TopicId={fixedTopicId}（共 {sources.Count} 个源）");
            }
            else if (classifyBySource)
            {
                _logger.Log($"目标已开启话题功能，将按源频道创建主题分类（共 {sources.Count} 个源）");
            }
        }
        else if (sources.Count > 1)
        {
            _logger.Log($"共 {sources.Count} 个源，将依次转发到目标主聊天（未启用话题）");
        }

        // 预解析用户自定义标签
        string? customTagsText = ParseTags(tags);
        if (!string.IsNullOrEmpty(customTagsText))
        {
            _logger.Log($"自定义标签: {customTagsText}");
        }

        // 主题 ID 缓存：避免同一源重复创建/查询主题（仅 classifyBySource 模式使用）
        var topicCache = new Dictionary<long, long>();

        int grandTotalForwarded = 0;
        for (int i = 0; i < sources.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var (link, sourceIdOverride) = sources[i];

            _logger.Log($"━━━ 处理源 [{i + 1}/{sources.Count}]: {link} ━━━");

            var (sourceChatId, startMessageId) = await ResolveSourceLinkAsync(link);
            if (sourceChatId == 0)
            {
                _logger.Log($"无法解析源链接: {link}，跳过");
                continue;
            }

            // 优先使用行内 sourceId，否则回退到全局 fallbackSourceId
            var effectiveSourceId = sourceIdOverride
                ?? (long.TryParse(fallbackSourceId, out var gsid) ? gsid : 0);
            if (effectiveSourceId > 0)
            {
                startMessageId = effectiveSourceId;
            }

            // 决定目标主题
            long messageThreadId = 0;
            if (hasFixedTopic)
            {
                // 固定话题模式：所有源共用同一 topicId
                messageThreadId = fixedTopicId;
            }
            else if (classifyBySource && isForum)
            {
                if (!topicCache.TryGetValue(sourceChatId, out messageThreadId))
                {
                    var sourceChat = await client.GetChatAsync(sourceChatId);
                    var topicName = sourceChat.Title;
                    messageThreadId = await CreateOrFindForumTopicAsync(targetChatId, topicName);
                    topicCache[sourceChatId] = messageThreadId;
                }

                if (messageThreadId == 0)
                {
                    _logger.Log($"无法为目标创建主题，跳过该源");
                    continue;
                }
            }

            // 计算本源的有效标签：自定义优先，否则回退到源链接提取的默认标签
            string? effectiveTags = customTagsText ?? ExtractDefaultTagFromLink(link);
            if (!string.IsNullOrEmpty(effectiveTags))
            {
                _logger.Log($"本源标签: {effectiveTags}");
            }

            using var db = CreateForwardDbContext(sourceChatId);
            await db.Database.EnsureCreatedAsync();
            _logger.Log($"数据库已就绪: forward-{sourceChatId}.db");

            int forwarded;
            if (older)
            {
                forwarded = await ForwardOlderDirection(db, sourceChatId, startMessageId, targetChatId, limit, forwardComments, ct, messageThreadId, effectiveTags);
            }
            else
            {
                forwarded = await ForwardNewerDirection(db, sourceChatId, startMessageId, targetChatId, limit, forwardComments, ct, messageThreadId, effectiveTags);
            }

            grandTotalForwarded += forwarded;
            _logger.Log($"源 [{link}] 转发完成: {forwarded} 条");
        }

        _logger.Log($"全部源处理完成，共转发 {grandTotalForwarded} 条消息");
    }

    public async Task<int> DeepCopyForward(ForwardDbContext db, long sourceChatId, long startMessageId, long targetChatId, int limit, bool forwardComments, CancellationToken ct = default, long messageThreadId = 0)
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

                var (forwarded, skipped) = await ForwardGroupedMessages(db, toProcess, sourceChatId, targetChatId, forwardComments, ct, messageThreadId);
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
            var (forwarded, skipped) = await ForwardGroupedMessages(db, pendingGroup, sourceChatId, targetChatId, forwardComments, ct, messageThreadId);
            totalForwarded += forwarded;
            totalSkipped += skipped;
        }

        if (totalSkipped > 0)
        {
            _logger.Log($"跳过已转发消息 {totalSkipped} 条");
        }

        return totalForwarded;
    }

    public async Task<int> ForwardOlderDirection(ForwardDbContext db, long sourceChatId, long startMessageId, long targetChatId, int limit, bool forwardComments, CancellationToken ct = default, long messageThreadId = 0, string? tags = null)
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

                var (forwarded, skipped) = await ForwardGroupedMessages(db, toProcess, sourceChatId, targetChatId, forwardComments, ct, messageThreadId, tags);
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
            var (forwarded, skipped) = await ForwardGroupedMessages(db, pendingGroup, sourceChatId, targetChatId, forwardComments, ct, messageThreadId, tags);
            totalForwarded += forwarded;
            totalSkipped += skipped;
        }

        if (totalSkipped > 0)
        {
            _logger.Log($"跳过已转发消息 {totalSkipped} 条");
        }

        return totalForwarded;
    }

    public async Task<int> ForwardNewerDirection(ForwardDbContext db, long sourceChatId, long startMessageId, long targetChatId, int limit, bool forwardComments, CancellationToken ct = default, long messageThreadId = 0, string? tags = null)
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

        var (totalForwarded, totalSkipped) = await ForwardGroupedMessages(db, newerMessages, sourceChatId, targetChatId, forwardComments, ct, messageThreadId, tags);

        if (totalSkipped > 0)
        {
            _logger.Log($"跳过已转发消息 {totalSkipped} 条");
        }

        return totalForwarded;
    }

    async Task<(int forwarded, int skipped)> ForwardGroupedMessages(ForwardDbContext db, List<TdApi.Message> messages, long sourceChatId, long targetChatId, bool forwardComments, CancellationToken ct = default, long messageThreadId = 0, string? tags = null)
    {
        if (messages.Count == 0) return (0, 0);

        int totalForwarded = 0;
        int totalSkipped = 0;
        var groups = GroupMessagesByAlbum(messages);

        // 当指定 messageThreadId 时，构造论坛主题 MessageTopic
        TdApi.MessageTopic? messageTopic = messageThreadId > 0
            ? new TdApi.MessageTopic.MessageTopicForum { ForumTopicId = (int)messageThreadId }
            : null;

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
                        topicId: messageTopic,
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
                        await ForwardCommentsForMessages(db, sourceChatId, targetChatId, forwardedMessages, result.Messages_, ct, messageThreadId, tags);
                    }

                    // 在转发消息的 caption/text 中追加标签（仅当指定 tags 时）
                    if (!string.IsNullOrEmpty(tags) && result.Messages_ != null && result.Messages_.Length > 0)
                    {
                        await AppendTagsToForwardedMessagesAsync(targetChatId, messageThreadId, result.Messages_, tags, ct);
                        await Task.Delay(800, ct);
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

    /// <summary>
    /// 在已转发消息的 caption/text 末尾追加标签文本。
    /// 优先编辑第一条有 caption/text 的消息（相册中通常仅首条有 caption）；
    /// 若所有消息均无可编辑文本则回退为发送独立标签消息。
    /// 注意：ForwardMessagesAsync 返回的 Messages_ 包含本地（临时）ID，
    /// 服务器确认后本地 ID 失效，必须通过 TryGetServerMessageId 映射到服务器 ID。
    /// </summary>
    async Task AppendTagsToForwardedMessagesAsync(
        long targetChatId, long messageThreadId, TdApi.Message[] forwardedMessages,
        string tagsText, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tagsText) || forwardedMessages.Length == 0) return;

        foreach (var msg in forwardedMessages)
        {
            ct.ThrowIfCancellationRequested();

            var (text, isTextMessage) = GetEditableText(msg);
            if (text == null) continue;

            // 将本地消息 ID 映射为服务器消息 ID（最多等待 5 秒）
            var localId = msg.Id;
            if (!TryGetServerMessageId(localId, out var serverId))
            {
                _logger.Log($"等待消息 {localId} 的服务器 ID 映射...");
                for (int i = 0; i < 10; i++)
                {
                    await Task.Delay(500, ct);
                    if (TryGetServerMessageId(localId, out serverId)) break;
                }
            }

            if (serverId == 0)
            {
                _logger.Log($"未获取到消息 {localId} 的服务器 ID，跳过标签编辑");
                continue;
            }

            try
            {
                var newText = AppendTagsToFormattedText(text, tagsText);

                if (isTextMessage)
                {
                    var inputText = new TdApi.InputMessageContent.InputMessageText
                    {
                        Text = newText,
                        LinkPreviewOptions = null,
                        ClearDraft = false
                    };
                    await Client.EditMessageTextAsync(
                        chatId: targetChatId,
                        messageId: serverId,
                        replyMarkup: null,
                        inputMessageContent: inputText
                    );
                }
                else
                {
                    await Client.EditMessageCaptionAsync(
                        chatId: targetChatId,
                        messageId: serverId,
                        replyMarkup: null,
                        caption: newText
                    );
                }

                _logger.Log($"已在转发消息 {serverId} 的文本中追加标签: {tagsText}");
                return; // 只编辑第一条有文本的消息
            }
            catch (TdException ex)
            {
                _logger.Log($"编辑消息标签失败: MsgId={serverId}, 错误: {ex.Error.Message}");
            }
            catch (Exception ex)
            {
                _logger.Log($"编辑消息标签异常: MsgId={serverId}, 错误: {ex.Message}");
            }
        }

        // 所有消息都无法编辑，回退为发送独立标签消息
        _logger.Log("无可编辑文本的消息，回退为发送独立标签消息");
        await SendTagMessageAsync(targetChatId, messageThreadId, tagsText, ct);
    }

    /// <summary>
    /// 从消息内容中提取可编辑的文本（caption 或 text）。
    /// 返回 (FormattedText, isTextMessage)：isTextMessage=true 时用 EditMessageText，否则用 EditMessageCaption。
    /// </summary>
    static (TdApi.FormattedText? text, bool isTextMessage) GetEditableText(TdApi.Message msg)
    {
        return msg.Content switch
        {
            TdApi.MessageContent.MessageText t => (t.Text, true),
            TdApi.MessageContent.MessagePhoto p => (p.Caption, false),
            TdApi.MessageContent.MessageVideo v => (v.Caption, false),
            TdApi.MessageContent.MessageDocument d => (d.Caption, false),
            TdApi.MessageContent.MessageAnimation a => (a.Caption, false),
            TdApi.MessageContent.MessageAudio aud => (aud.Caption, false),
            TdApi.MessageContent.MessageVoiceNote vn => (vn.Caption, false),
            _ => (null, false)
        };
    }

    /// <summary>
    /// 在 FormattedText 末尾追加标签文本，保留原有 entities（偏移量不变）。
    /// </summary>
    static TdApi.FormattedText AppendTagsToFormattedText(TdApi.FormattedText original, string tagsText)
    {
        var newText = string.IsNullOrEmpty(original.Text)
            ? tagsText
            : original.Text + "\n\n" + tagsText;

        return new TdApi.FormattedText
        {
            Text = newText,
            Entities = original.Entities ?? []
        };
    }

    async Task ForwardCommentsForMessages(ForwardDbContext db, long sourceChatId, long targetChatId, List<TdApi.Message> sourceMessages, TdApi.Message[] forwardedMessages, CancellationToken ct = default, long messageThreadId = 0, string? tags = null)
    {
        TdApi.MessageTopic? messageTopic = messageThreadId > 0
            ? new TdApi.MessageTopic.MessageTopicForum { ForumTopicId = (int)messageThreadId }
            : null;

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
                         topicId: messageTopic,
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

    // ===== Merged from TdlService.SingleForward.cs =====
    /// <summary>
    /// 单条消息深度转发。目标支持频道或群聊；当 topicName 非空时，目标必须为开启话题的论坛超级群组，
    /// 消息会转发到该名称的主题（不存在时自动创建）。
    /// </summary>
    public async Task SingleForwardAsync(string sourceLink, string targetLink,
        bool forwardComments, string? topicName = null, CancellationToken ct = default)
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

        // 解析目标话题（仅在指定 topicName 时启用）
        long messageThreadId = 0;
        TdApi.MessageTopic? messageTopic = null;
        if (!string.IsNullOrWhiteSpace(topicName))
        {
            if (!await IsForumChatAsync(targetChatId))
            {
                _logger.Log($"目标 [{targetChat.Title}] 不是论坛超级群组，无法使用话题功能。请在 Telegram 中将目标群组开启「话题」功能后重试。");
                return;
            }

            messageThreadId = await CreateOrFindForumTopicAsync(targetChatId, topicName!);
            if (messageThreadId == 0)
            {
                _logger.Log($"无法解析或创建话题 [{topicName}]，终止转发");
                return;
            }
            messageTopic = new TdApi.MessageTopic.MessageTopicForum { ForumTopicId = (int)messageThreadId };
            _logger.Log($"目标话题: [{topicName}] TopicId={messageThreadId}");
        }

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
                        topicId: messageTopic,
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
                        await ForwardCommentsForMessages(db, sourceChatId, targetChatId, forwardedMessages, result.Messages_, ct, messageThreadId);
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

    // ===== Merged from TdlService.DeleteForwards.cs =====
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
