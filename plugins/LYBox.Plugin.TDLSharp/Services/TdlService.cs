using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TdLib;

namespace LYBox.Plugin.TDLSharp.Services;

public partial class TdlService
{
    readonly TdlClientManager _clientManager;
    readonly DirectLogger _logger;

    readonly Dictionary<long, TaskCompletionSource<TdApi.Error?>> _pendingSends = new();
    readonly object _pendingLock = new();

    /// <summary>
    /// 本地消息 ID → 服务器消息 ID 的映射。
    /// TDLib 发送消息时先返回本地（临时）ID，服务器确认后通过 UpdateMessageSendSucceeded
    /// 报告最终服务器 ID 和对应的 OldMessageId（本地 ID）。本地 ID 之后不可用。
    /// </summary>
    readonly Dictionary<long, long> _localToServerMsgId = new();
    readonly object _msgIdMapLock = new();

    public TdlService(TdlClientManager clientManager, DirectLogger logger)
    {
        _clientManager = clientManager;
        _logger = logger;
        _clientManager.RegisterMessageUpdateHandler(HandleMessageUpdateAsync);
    }

    /// <summary>
    /// 处理 TDLib 消息更新：追踪发送成功/失败，记录本地→服务器消息 ID 映射。
    /// </summary>
    async Task HandleMessageUpdateAsync(TdApi.Update update)
    {
        switch (update)
        {
            case TdApi.Update.UpdateMessageSendSucceeded umss:
                RecordMessageIdMapping(umss.OldMessageId, umss.Message.Id);
                RemovePendingSend(umss.OldMessageId);
                break;
            case TdApi.Update.UpdateMessageSendFailed umsf:
                NotifySendFailed(umsf.Message.Id, umsf.Error);
                break;
        }
        await Task.CompletedTask;
    }

    void RecordMessageIdMapping(long localId, long serverId)
    {
        lock (_msgIdMapLock)
        {
            _localToServerMsgId[localId] = serverId;
        }
    }

    /// <summary>
    /// 查询本地消息 ID 对应的服务器消息 ID。
    /// 返回 true 表示映射已存在，serverId 为最终服务器 ID；false 表示尚未收到发送确认。
    /// </summary>
    public bool TryGetServerMessageId(long localId, out long serverId)
    {
        lock (_msgIdMapLock)
        {
            return _localToServerMsgId.TryGetValue(localId, out serverId);
        }
    }

    [GeneratedRegex(@"(?:https?:\/\/)?(?:t\.me|telegram\.me)\/(?<name>[^\/\?\#]+)", RegexOptions.IgnoreCase)]
    private static partial Regex TelegramLinkRegex();

    [GeneratedRegex(@"(\d+)")]
    private static partial Regex DigitRegex();

    public TdClient Client => _clientManager.Client;

    public async Task EnsureReadyAsync()
    {
        await _clientManager.EnsureInitializedAsync();
        await _clientManager.WaitReadyAsync();
    }

    public async Task<TdApi.User> GetCurrentUserAsync()
    {
        return await _clientManager.GetCurrentUserAsync();
    }

    public async Task<(long chatId, long messageId)> ResolveSourceLinkAsync(string link)
    {
        var client = Client;
        try
        {
            var linkInfo = await client.GetMessageLinkInfoAsync(link);
            if (linkInfo.Message != null)
            {
                return (linkInfo.Message.ChatId, linkInfo.Message.Id);
            }
            _logger.Log($"源链接未关联到消息: {link}");
        }
        catch (TdException ex)
        {
            _logger.Log($"无法解析源链接: {link} - {ex.Message}");
        }
        return (0, 0);
    }

    public async Task<long> ResolveTargetLinkAsync(string link)
    {
        var client = Client;
        try
        {
            var linkInfo = await client.GetMessageLinkInfoAsync(link);
            if (linkInfo.Message != null)
            {
                return linkInfo.Message.ChatId;
            }
        }
        catch (TdException ex) { Debug.WriteLine($"[TdlService] 链接解析尝试失败: {ex.Message}"); }

        try
        {
            if (IsInviteLink(link))
            {
                var inviteInfo = await client.CheckChatInviteLinkAsync(link);
                if (inviteInfo.ChatId != 0)
                {
                    _logger.Log($"邀请链接已关联到 ChatId: {inviteInfo.ChatId}");
                    return inviteInfo.ChatId;
                }
                _logger.Log($"邀请链接有效但未关联到已有聊天: {link}");
                return 0;
            }
        }
        catch (TdException ex)
        {
            _logger.Log($"无法解析邀请链接: {link} - {ex.Message}");
            return 0;
        }

        try
        {
            var username = ExtractUsername(link);
            if (!string.IsNullOrEmpty(username))
            {
                var chat = await client.SearchPublicChatAsync(username);
                if (chat != null)
                {
                    return chat.Id;
                }
            }
        }
        catch (TdException ex) { Debug.WriteLine($"[TdlService] 链接解析尝试失败: {ex.Message}"); }

        if (long.TryParse(link.Trim(), out long chatId))
        {
            return chatId;
        }

        try
        {
            var foundChatId = await SearchChatByTitleAsync(link);
            if (foundChatId != 0)
            {
                return foundChatId;
            }
        }
        catch (TdException ex) { Debug.WriteLine($"[TdlService] 链接解析尝试失败: {ex.Message}"); }

        _logger.Log($"目标链接未关联到聊天: {link}");
        return 0;
    }

    public async Task<long> ResolveChatIdAsync(string? link)
    {
        if (string.IsNullOrWhiteSpace(link)) return 0;
        var client = Client;

        try
        {
            var linkInfo = await client.GetMessageLinkInfoAsync(link);
            if (linkInfo.Message != null)
            {
                return linkInfo.Message.ChatId;
            }
        }
        catch (TdException ex) { Debug.WriteLine($"[TdlService] 链接解析尝试失败: {ex.Message}"); }

        try
        {
            if (IsInviteLink(link))
            {
                var inviteInfo = await client.CheckChatInviteLinkAsync(link);
                if (inviteInfo.ChatId != 0)
                {
                    _logger.Log($"邀请链接已关联到 ChatId: {inviteInfo.ChatId}");
                    return inviteInfo.ChatId;
                }
                return 0;
            }
        }
        catch (TdException ex) { Debug.WriteLine($"[TdlService] 链接解析尝试失败: {ex.Message}"); }

        try
        {
            var username = ExtractUsername(link);
            if (!string.IsNullOrEmpty(username))
            {
                var chat = await client.SearchPublicChatAsync(username);
                if (chat != null)
                {
                    return chat.Id;
                }
            }
        }
        catch (TdException ex) { Debug.WriteLine($"[TdlService] 链接解析尝试失败: {ex.Message}"); }

        if (long.TryParse(link.Trim(), out long chatId))
        {
            return chatId;
        }

        try
        {
            var chatIds = await client.GetChatsAsync(limit: 200);
            if (chatIds?.ChatIds != null)
            {
                foreach (var id in chatIds.ChatIds)
                {
                    try
                    {
                        var chat = await client.GetChatAsync(id);
                        if (chat.Title.Contains(link, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.Log($"找到匹配聊天: [{chat.Title}] ChatId={chat.Id}");
                            return chat.Id;
                        }
                    }
                    catch (Exception ex) { Debug.WriteLine($"[TdlService] 搜索聊天时获取单个聊天失败 ChatId={id}: {ex.Message}"); }
                }
            }
        }
        catch (Exception ex) { Debug.WriteLine($"[TdlService] 搜索聊天列表失败: {ex.Message}"); }

        return 0;
    }

    async Task<long> SearchChatByTitleAsync(string keyword)
    {
        _logger.Log($"在聊天列表中搜索: {keyword}");
        var client = Client;
        var chatIds = await client.GetChatsAsync(limit: 200);
        if (chatIds?.ChatIds == null) return 0;

        foreach (var id in chatIds.ChatIds)
        {
            try
            {
                var chat = await client.GetChatAsync(id);
                if (chat.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.Log($"找到匹配聊天: [{chat.Title}] ChatId={chat.Id}");
                    return chat.Id;
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[TdlService] 按标题搜索时获取单个聊天失败 ChatId={id}: {ex.Message}"); }
        }

        return 0;
    }

    bool IsInviteLink(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        input = input.Trim();
        if (input.StartsWith("https://t.me/+", StringComparison.OrdinalIgnoreCase)) return true;
        if (input.StartsWith("https://t.me/joinchat/", StringComparison.OrdinalIgnoreCase)) return true;
        if (input.StartsWith("https://telegram.me/+", StringComparison.OrdinalIgnoreCase)) return true;
        if (input.StartsWith("https://telegram.me/joinchat/", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    string? ExtractUsername(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        input = input.Trim();
        if (input.StartsWith("@")) return input.Substring(1);
        if (!input.Contains("/")) return null;

        var match = TelegramLinkRegex().Match(input);

        if (!match.Success) return null;
        var name = match.Groups["name"].Value;
        if (name.StartsWith("+")) return null;
        return name;
    }

    int ParseRetryAfter(TdException ex)
    {
        if (ex.Error?.Message != null)
        {
            return ParseRetryAfterFromMessage(ex.Error.Message);
        }
        return 15;
    }

    int ParseRetryAfterFromError(TdApi.Error error)
    {
        if (error?.Message != null)
        {
            return ParseRetryAfterFromMessage(error.Message);
        }
        return 15;
    }

    int ParseRetryAfterFromMessage(string message)
    {
        var match = DigitRegex().Match(message);
        if (match.Success && int.TryParse(match.Groups[1].Value, out int seconds) && seconds > 0)
        {
            return Math.Min(seconds + 2, 300);
        }
        return 15;
    }

    List<List<TdApi.Message>> GroupMessagesByAlbum(List<TdApi.Message> messages)
    {
        var result = new List<List<TdApi.Message>>();
        if (messages.Count == 0) return result;

        var currentGroup = new List<TdApi.Message> { messages[0] };
        long currentAlbumId = messages[0].MediaAlbumId;

        for (int i = 1; i < messages.Count; i++)
        {
            if (messages[i].MediaAlbumId != 0 && messages[i].MediaAlbumId == currentAlbumId)
            {
                currentGroup.Add(messages[i]);
            }
            else
            {
                result.Add(currentGroup);
                currentGroup = [messages[i]];
                currentAlbumId = messages[i].MediaAlbumId;
            }
        }

        result.Add(currentGroup);
        return result;
    }

    (List<TdApi.Message> toProcess, List<TdApi.Message>? pending) ExtractPendingMediaGroup(List<TdApi.Message> messages)
    {
        if (messages.Count == 0) return (messages, null);

        var lastMsg = messages[^1];
        if (lastMsg.MediaAlbumId == 0) return (messages, null);

        var pending = new List<TdApi.Message>();
        for (int i = messages.Count - 1; i >= 0; i--)
        {
            if (messages[i].MediaAlbumId == lastMsg.MediaAlbumId)
            {
                pending.Insert(0, messages[i]);
            }
            else
            {
                break;
            }
        }

        var toProcess = messages.Take(messages.Count - pending.Count).ToList();
        return (toProcess, pending);
    }

    void RegisterPendingSend(long messageId)
    {
        lock (_pendingLock)
        {
            if (!_pendingSends.ContainsKey(messageId))
            {
                _pendingSends[messageId] = new TaskCompletionSource<TdApi.Error?>();
            }
        }
    }

    void RemovePendingSend(long messageId)
    {
        lock (_pendingLock)
        {
            if (_pendingSends.TryGetValue(messageId, out var tcs))
            {
                tcs.TrySetResult(null);
                _pendingSends.Remove(messageId);
            }
        }
    }

    void NotifySendFailed(long messageId, TdApi.Error error)
    {
        lock (_pendingLock)
        {
            if (_pendingSends.TryGetValue(messageId, out var tcs))
            {
                tcs.TrySetResult(error);
                _pendingSends.Remove(messageId);
            }
        }
    }

    async Task<TdApi.Error?> WaitForSendResultAsync(long[] messageIds, int timeoutSeconds = 3)
    {
        TaskCompletionSource<TdApi.Error?>[] tcsArray;
        lock (_pendingLock)
        {
            tcsArray = new TaskCompletionSource<TdApi.Error?>[messageIds.Length];
            for (int i = 0; i < messageIds.Length; i++)
            {
                if (!_pendingSends.TryGetValue(messageIds[i], out var tcs))
                {
                    _pendingSends[messageIds[i]] = new TaskCompletionSource<TdApi.Error?>();
                }
                tcsArray[i] = _pendingSends[messageIds[i]];
            }
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            var allTasks = tcsArray.Select(tcs => tcs.Task).ToArray();
            var completed = await Task.WhenAny(Task.WhenAll(allTasks), Task.Delay(timeoutSeconds * 1000, cts.Token));

            foreach (var tcs in tcsArray)
            {
                if (tcs.Task.IsCompleted && tcs.Task.Result != null)
                {
                    return tcs.Task.Result;
                }
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    ForwardDbContext CreateForwardDbContext(long chatId)
    {
        Directory.CreateDirectory(TdlPaths.ForwardDbDir);
        return new ForwardDbContext(chatId, TdlPaths.ForwardDbDir);
    }

    /// <summary>
    /// 检查目标聊天是否为论坛超级群组（支持 Topics）。
    /// </summary>
    public async Task<bool> IsForumChatAsync(long chatId)
    {
        try
        {
            var chat = await Client.GetChatAsync(chatId);
            if (chat.Type is TdApi.ChatType.ChatTypeSupergroup super)
            {
                var superInfo = await Client.GetSupergroupAsync(super.SupergroupId);
                return superInfo.IsForum;
            }
        }
        catch (Exception ex) { Debug.WriteLine($"[TdlService] 检查论坛状态失败 ChatId={chatId}: {ex.Message}"); }
        return false;
    }

    /// <summary>
    /// 在目标论坛中按名称查找或创建主题，返回 ForumTopicId（0 表示失败）。
    /// 已存在同名主题时复用，避免重复创建。
    /// </summary>
    public async Task<long> CreateOrFindForumTopicAsync(long targetChatId, string topicName)
    {
        if (string.IsNullOrWhiteSpace(topicName)) return 0;

        // Telegram 主题名称限制 1-128 字符
        var name = topicName.Trim();
        if (name.Length > 128) name = name[..128];

        try
        {
            // 先搜索现有主题，避免重复创建
            var found = await Client.GetForumTopicsAsync(
                chatId: targetChatId,
                query: name,
                offsetDate: 0,
                offsetMessageId: 0,
                offsetForumTopicId: 0,
                limit: 100
            );

            if (found.Topics != null)
            {
                var existing = found.Topics.FirstOrDefault(t =>
                    string.Equals(t.Info?.Name, name, StringComparison.OrdinalIgnoreCase));
                if (existing?.Info != null)
                {
                    _logger.Log($"复用现有主题: [{existing.Info.Name}] TopicId={existing.Info.ForumTopicId}");
                    return existing.Info.ForumTopicId;
                }
            }
        }
        catch (Exception ex) { Debug.WriteLine($"[TdlService] 搜索主题失败: {ex.Message}"); }

        try
        {
            var topicInfo = await Client.CreateForumTopicAsync(
                chatId: targetChatId,
                name: name,
                isNameImplicit: false,
                icon: null
            );
            _logger.Log($"已创建主题: [{name}] TopicId={topicInfo.ForumTopicId}");
            return topicInfo.ForumTopicId;
        }
        catch (TdException ex)
        {
            _logger.Log($"创建主题失败: [{name}] - {ex.Error.Message}");
            return 0;
        }
        catch (Exception ex)
        {
            _logger.Log($"创建主题异常: [{name}] - {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// 解析多行源链接输入，每行一个源。支持 "link|sourceId" 格式指定起始消息ID。
    /// </summary>
    public static List<(string link, long? sourceId)> ParseMultiLineSources(string? raw)
    {
        var result = new List<(string link, long? sourceId)>();
        if (string.IsNullOrWhiteSpace(raw)) return result;

        foreach (var line in raw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            long? sourceId = null;
            var link = trimmed;

            var pipeIdx = trimmed.IndexOf('|');
            if (pipeIdx > 0 && long.TryParse(trimmed[(pipeIdx + 1)..].Trim(), out var sid))
            {
                link = trimmed[..pipeIdx].Trim();
                sourceId = sid;
            }

            if (!string.IsNullOrWhiteSpace(link))
            {
                result.Add((link, sourceId));
            }
        }

        return result
            .GroupBy(s => s.link, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    string? BuildSourceMessageUrl(TdApi.Message msg)
    {
        if (msg.ForwardInfo == null) return null;

        long originChatId = 0;
        long originMessageId = 0;

        if (msg.ForwardInfo.Origin is TdApi.MessageOrigin.MessageOriginChannel oc)
        {
            originChatId = oc.ChatId;
            originMessageId = oc.MessageId;
        }
        else if (msg.ForwardInfo.Origin is TdApi.MessageOrigin.MessageOriginUser ou)
        {
            return null;
        }
        else if (msg.ForwardInfo.Source != null)
        {
            originChatId = msg.ForwardInfo.Source.ChatId;
            originMessageId = msg.ForwardInfo.Source.MessageId;
        }

        if (originChatId == 0 || originMessageId == 0) return null;

        string chatPrefix = originChatId < 0
            ? $"c/{Math.Abs(originChatId) % 1000000000000L}"
            : originChatId.ToString();

        return $"https://t.me/{chatPrefix}/{originMessageId}";
    }
    string? BuildTargetMessageUrl(TdApi.Message msg)
    {

        string chatPrefix = msg.ChatId < 0
            ? $"c/{Math.Abs(msg.ChatId) % 1000000000000L}"
            : msg.ChatId.ToString();

        return $"https://t.me/{chatPrefix}/{msg.Id}";
    }

    /// <summary>
    /// 解析用户输入的标签文本（逗号分隔），自动添加 # 前缀。
    /// 已以 # 开头的标签不重复添加。空标签会被忽略。
    /// 例: "tag1, #tag2, tag3" → "#tag1 #tag2 #tag3"
    /// </summary>
    public static string? ParseTags(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var tags = raw.Split([',', '，', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.StartsWith('#') ? s : "#" + s)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return tags.Count == 0 ? null : string.Join(" ", tags);
    }

    /// <summary>
    /// 从源链接提取默认标签。优先提取 @username，其次从 t.me 链接提取 username，
    /// 提取失败时返回 null（由调用方决定回退策略）。
    /// 例: "@channel" → "#channel"；"https://t.me/channel/123" → "#channel"
    /// </summary>
    public static string? ExtractDefaultTagFromLink(string? link)
    {
        if (string.IsNullOrWhiteSpace(link)) return null;

        var trimmed = link.Trim();

        // @username 形式
        if (trimmed.StartsWith('@'))
        {
            var name = trimmed[1..].Trim();
            if (!string.IsNullOrWhiteSpace(name)) return "#" + SanitizeTag(name);
        }

        // t.me/username 或 telegram.me/username 形式
        var match = TelegramLinkRegex().Match(trimmed);
        if (match.Success)
        {
            var name = match.Groups["name"].Value;
            if (!string.IsNullOrWhiteSpace(name) && !name.StartsWith('+'))
            {
                return "#" + SanitizeTag(name);
            }
        }

        return null;
    }

    /// <summary>
    /// 清理标签内容：仅保留字母、数字、下划线，其他字符替换为下划线，去除首尾下划线。
    /// </summary>
    static string SanitizeTag(string raw)
    {
        var sanitized = new StringBuilder(raw.Length);
        foreach (var ch in raw)
        {
            if (char.IsLetterOrDigit(ch) || ch == '_')
            {
                sanitized.Append(ch);
            }
            else if (sanitized.Length > 0 && sanitized[^1] != '_')
            {
                sanitized.Append('_');
            }
        }
        return sanitized.ToString().Trim('_');
    }

    /// <summary>
    /// 发送一条独立的标签消息到指定聊天/话题。用于在转发消息后追加自定义标签。
    /// 失败时不影响主流程，仅记录日志。
    /// </summary>
    public async Task SendTagMessageAsync(long targetChatId, long messageThreadId, string tagsText, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tagsText)) return;
        if (ct.IsCancellationRequested) return;

        try
        {
            TdApi.MessageTopic? messageTopic = messageThreadId > 0
                ? new TdApi.MessageTopic.MessageTopicForum { ForumTopicId = (int)messageThreadId }
                : null;

            var inputText = new TdApi.InputMessageContent.InputMessageText
            {
                Text = new TdApi.FormattedText
                {
                    Text = tagsText,
                    Entities = Array.Empty<TdApi.TextEntity>()
                },
                LinkPreviewOptions = null,
                ClearDraft = false
            };

            var sent = await Client.SendMessageAsync(
                chatId: targetChatId,
                topicId: messageTopic,
                replyTo: null,
                options: null,
                replyMarkup: null,
                inputMessageContent: inputText
            );

            _logger.Log($"已追加标签消息: {tagsText}");
        }
        catch (TdException ex)
        {
            _logger.Log($"发送标签消息失败: {ex.Error.Message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TdlService] 发送标签消息异常: {ex.Message}");
        }
    }
}

