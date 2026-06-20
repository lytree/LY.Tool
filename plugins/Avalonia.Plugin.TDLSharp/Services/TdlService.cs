using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TdLib;

namespace Avalonia.Plugin.TDLSharp.Services;

public partial class TdlService
{
    readonly TdlClientManager _clientManager;
    readonly DirectUiLogger _logger;

    readonly Dictionary<long, TaskCompletionSource<TdApi.Error?>> _pendingSends = new();
    readonly object _pendingLock = new();

    public TdlService(TdlClientManager clientManager, DirectUiLogger logger)
    {
        _clientManager = clientManager;
        _logger = logger;
    }

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

        var match = Regex.Match(input,
            @"(?:https?:\/\/)?(?:t\.me|telegram\.me)\/(?<name>[^\/\?\#]+)",
            RegexOptions.IgnoreCase);

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
        var match = Regex.Match(message, @"(\d+)");
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
        var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AvaloniaTemplate", "TDLSharp", "data");
        Directory.CreateDirectory(dataDir);
        return new ForwardDbContext(chatId, dataDir);
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
}
