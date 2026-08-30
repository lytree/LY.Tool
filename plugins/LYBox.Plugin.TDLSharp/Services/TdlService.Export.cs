using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using TdLib;

namespace LYBox.Plugin.TDLSharp.Services;

public partial class TdlService
{
    public async Task ExportMessagesAsync(string channelLink, string? outputPath, bool exportComments, int limit, CancellationToken ct = default)
    {
        await EnsureReadyAsync();

        var client = Client;

        long chatId = await ResolveChatIdAsync(channelLink);
        if (chatId == 0)
        {
            _logger.Log($"无法解析频道: {channelLink}");
            return;
        }

        var chat = await client.GetChatAsync(chatId);
        _logger.Log($"目标: [{chat.Title}] ChatId={chatId}");

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            string saveDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "tdl", "message");
            Directory.CreateDirectory(saveDir);
            outputPath = Path.Combine(saveDir, $"{chatId}.json");
        }

        var exportResult = await ExportChannelMessages(client, chatId, exportComments, limit, ct);

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        string json = JsonSerializer.Serialize(exportResult, jsonOptions);

        string? dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllTextAsync(outputPath, json);
        _logger.Log($"导出完成，共 {exportResult.TotalMessages} 条消息，{exportResult.Groups.Count} 个分组");
        _logger.Log($"文件已保存到: {outputPath}");
    }

    async Task<ChannelExport> ExportChannelMessages(TdClient client, long chatId, bool exportComments, int limit, CancellationToken ct)
    {
        long fromMessageId = 0;
        bool hasMore = true;
        // 不再一次性将所有消息加载到 allMessages，改为流式处理：
        // 每批拉取后立即分组并写入 export，避免大频道导出时内存峰值过高。
        var export = new ChannelExport
        {
            ChatId = chatId,
        };
        int totalCount = 0;

        _logger.Log("开始导出频道消息...");

        while (hasMore)
        {
            ct.ThrowIfCancellationRequested();
            List<TdApi.Message>? batch = null;
            try
            {
                var history = await client.GetChatHistoryAsync(chatId, fromMessageId, 0, 100, false);
                if (history.Messages_ == null || history.Messages_.Length == 0)
                {
                    hasMore = false;
                    break;
                }

                batch = [.. history.Messages_];
                totalCount += batch.Count;

                fromMessageId = batch[^1].Id;
                _logger.Log($"已拉取 {totalCount} 条消息，当前进度 ID: {fromMessageId}");

                if (limit > 0 && totalCount >= limit)
                {
                    hasMore = false;
                    // 截断到 limit
                    var excess = totalCount - limit;
                    if (excess > 0 && excess < batch.Count)
                    {
                        batch.RemoveRange(batch.Count - excess, excess);
                    }
                }

                await Task.Delay(300, ct);
            }
            catch (TdException ex) when (ex.Error.Code == 429)
            {
                int retryAfter = ParseRetryAfter(ex);
                _logger.Log($"触发频率限制，等待 {retryAfter} 秒后继续...");
                await Task.Delay(retryAfter * 1000, ct);
                continue;
            }
            catch (Exception ex)
            {
                _logger.Log($"拉取消息时发生异常: {ex.Message}");
                await Task.Delay(5000, ct);
                continue;
            }

            if (batch is null || batch.Count == 0) continue;

            // 立即处理本批消息：分组并写入 export，避免跨批持有引用
            var groups = GroupMessagesByAlbum(batch);

            foreach (var group in groups)
            {
                ct.ThrowIfCancellationRequested();
                var exportGroup = new MessageGroup
                {
                    MediaAlbumId = group[0].MediaAlbumId != 0 ? group[0].MediaAlbumId.ToString() : null,
                    IsGrouped = group.Count > 1 && group[0].MediaAlbumId != 0
                };

                foreach (var msg in group)
                {
                    var msgInfo = BuildMessageInfo(msg);

                    if (exportComments)
                    {
                        try
                        {
                            var comments = await client.GetMessageThreadHistoryAsync(
                                chatId: chatId,
                                messageId: msg.Id,
                                fromMessageId: 0,
                                offset: 0,
                                limit: 50
                            );

                            if (comments.Messages_ != null && comments.Messages_.Length > 0)
                            {
                                msgInfo.Comments = comments.Messages_.Select(BuildMessageInfo).ToList();
                            }
                        }
                        catch (TdException ex)
                        {
                            _logger.Log($"获取评论失败: MsgId={msg.Id}, 错误: {ex.Error.Message}");
                        }

                        await Task.Delay(200, ct);
                    }

                    exportGroup.Messages.Add(msgInfo);
                }

                export.Groups.Add(exportGroup);
            }

            _logger.Log($"已处理累计 {export.Groups.Sum(g => g.Messages.Count)} 条消息");
        }

        var chat = await client.GetChatAsync(chatId);
        export.ChatTitle = chat.Title;
        export.ExportTime = DateTime.UtcNow;
        export.TotalMessages = export.Groups.Sum(g => g.Messages.Count);

        return export;
    }

    MessageInfo BuildMessageInfo(TdApi.Message msg)
    {
        var info = new MessageInfo
        {
            MessageId = msg.Id,
            Date = DateTimeOffset.FromUnixTimeSeconds(msg.Date).DateTime,
            EditDate = msg.EditDate != 0 ? DateTimeOffset.FromUnixTimeSeconds(msg.EditDate).DateTime : null,
            Type = MessageContentInspector.GetTypeName(msg.Content),
            Text = MessageContentInspector.GetText(msg.Content),
            Media = GetMediaInfo(msg.Content),
            ForwardInfo = msg.ForwardInfo != null ? new ForwardInfoExport
            {
                FromChatId = msg.ForwardInfo.Source?.ChatId ?? 0,
                FromMessageId = msg.ForwardInfo.Source?.MessageId ?? 0,
                Date = msg.ForwardInfo.Date != 0 ? DateTimeOffset.FromUnixTimeSeconds(msg.ForwardInfo.Date).DateTime : null,
                Origin = msg.ForwardInfo.Origin switch
                {
                    TdApi.MessageOrigin.MessageOriginUser ou => $"User:{ou.SenderUserId}",
                    TdApi.MessageOrigin.MessageOriginChannel oc => $"Channel:{oc.ChatId}:{oc.MessageId}",
                    TdApi.MessageOrigin.MessageOriginHiddenUser ohu => $"Hidden:{ohu.SenderName}",
                    TdApi.MessageOrigin.MessageOriginChat oc => $"Chat:{oc.SenderChatId}",
                    _ => null
                }
            } : null
        };

        return info;
    }

    MediaInfo? GetMediaInfo(TdApi.MessageContent content)
    {
        return content switch
        {
            TdApi.MessageContent.MessagePhoto p => new MediaInfo
            {
                Type = MessageContentInspector.GetTypeName(content),
                FileId = p.Photo.Sizes.LastOrDefault()?.Photo.Id.ToString(),
                Width = p.Photo.Sizes.LastOrDefault()?.Width,
                Height = p.Photo.Sizes.LastOrDefault()?.Height,
                FileSize = p.Photo.Sizes.LastOrDefault()?.Photo.ExpectedSize
            },
            TdApi.MessageContent.MessageVideo v => new MediaInfo
            {
                Type = MessageContentInspector.GetTypeName(content),
                FileId = v.Video.Video_.Id.ToString(),
                FileName = v.Video.FileName,
                Width = v.Video.Width,
                Height = v.Video.Height,
                Duration = v.Video.Duration,
                MimeType = v.Video.MimeType,
                FileSize = v.Video.Video_.ExpectedSize
            },
            TdApi.MessageContent.MessageAudio a => new MediaInfo
            {
                Type = MessageContentInspector.GetTypeName(content),
                FileId = a.Audio.Audio_.Id.ToString(),
                FileName = a.Audio.FileName,
                Duration = a.Audio.Duration,
                MimeType = a.Audio.MimeType,
                FileSize = a.Audio.Audio_.ExpectedSize
            },
            TdApi.MessageContent.MessageDocument d => new MediaInfo
            {
                Type = MessageContentInspector.GetTypeName(content),
                FileId = d.Document.Document_.Id.ToString(),
                FileName = d.Document.FileName,
                MimeType = d.Document.MimeType,
                FileSize = d.Document.Document_.ExpectedSize
            },
            TdApi.MessageContent.MessageVoiceNote vn => new MediaInfo
            {
                Type = MessageContentInspector.GetTypeName(content),
                FileId = vn.VoiceNote.Voice.Id.ToString(),
                Duration = vn.VoiceNote.Duration,
                MimeType = vn.VoiceNote.MimeType,
                FileSize = vn.VoiceNote.Voice.ExpectedSize
            },
            TdApi.MessageContent.MessageVideoNote vn => new MediaInfo
            {
                Type = MessageContentInspector.GetTypeName(content),
                FileId = vn.VideoNote.Video.Id.ToString(),
                Duration = vn.VideoNote.Duration,
                FileSize = vn.VideoNote.Video.ExpectedSize
            },
            TdApi.MessageContent.MessageAnimation ani => new MediaInfo
            {
                Type = MessageContentInspector.GetTypeName(content),
                FileId = ani.Animation.Animation_.Id.ToString(),
                FileName = ani.Animation.FileName,
                Width = ani.Animation.Width,
                Height = ani.Animation.Height,
                Duration = ani.Animation.Duration,
                MimeType = ani.Animation.MimeType,
                FileSize = ani.Animation.Animation_.ExpectedSize
            },
            TdApi.MessageContent.MessageSticker s => new MediaInfo
            {
                Type = MessageContentInspector.GetTypeName(content),
                FileId = s.Sticker.Sticker_.Id.ToString(),
                Width = s.Sticker.Width,
                Height = s.Sticker.Height,
                FileSize = s.Sticker.Sticker_.ExpectedSize
            },
            _ => null
        };
    }

    // ===== Merged from TdlService.ListChats.cs =====
    public async Task ListChatsAsync(string? outputPath, CancellationToken ct = default)
    {
        await EnsureReadyAsync();

        var client = Client;

        _logger.Log("正在列出所有聊天...");

        var chats = new List<ChatInfo>();
        int limit = 200;
        bool hasMore = true;

        while (hasMore)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var result = await client.GetChatsAsync(limit: limit);
                if (result.ChatIds == null || result.ChatIds.Length == 0)
                {
                    hasMore = false;
                    break;
                }

                foreach (var chatId in result.ChatIds)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        var chat = await client.GetChatAsync(chatId);
                        chats.Add(BuildChatInfo(chat));
                    }
                    catch (Exception ex) { _logger.Log($"获取聊天 ChatId={chatId} 失败: {ex.Message}"); }
                }

                hasMore = result.ChatIds.Length == limit;
            }
            catch (Exception ex)
            {
                _logger.Log($"获取聊天列表时发生异常: {ex.Message}");
                hasMore = false;
            }
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            string saveDir = TdlPaths.DefaultChatsDir;
            Directory.CreateDirectory(saveDir);
            outputPath = Path.Combine(saveDir, "chats.json");
        }

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        string json = JsonSerializer.Serialize(chats, jsonOptions);

        string? dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllTextAsync(outputPath, json);
        _logger.Log($"已列出 {chats.Count} 个聊天");
        _logger.Log($"文件已保存到: {outputPath}");
    }

    ChatInfo BuildChatInfo(TdApi.Chat chat)
    {
        var info = new ChatInfo
        {
            Id = chat.Id,
            Title = chat.Title,
            Type = chat.Type.GetType().Name.Replace("ChatType", ""),
            LastMessage = chat.LastMessage != null ? BuildLastMessageInfo(chat.LastMessage) : null
        };

        return info;
    }

    LastMessageInfo? BuildLastMessageInfo(TdApi.Message msg)
    {
        return new LastMessageInfo
        {
            Id = msg.Id,
            Date = DateTimeOffset.FromUnixTimeSeconds(msg.Date).DateTime,
            Type = MessageContentInspector.GetTypeName(msg.Content)
        };
    }

    // ===== Merged from TdlService.ExportMembers.cs =====
    public async Task ExportMembersAsync(
        string chatLink,
        string? outputPath,
        bool raw,
        CancellationToken ct = default)
    {
        await EnsureReadyAsync();

        var client = Client;

        long chatId = await ResolveChatIdAsync(chatLink);
        if (chatId == 0)
        {
            _logger.Log($"无法解析聊天: {chatLink}");
            return;
        }

        var chat = await client.GetChatAsync(chatId);
        _logger.Log($"目标: [{chat.Title}] ChatId={chatId}");

        _logger.Log("开始导出成员...");

        var members = new List<MemberInfo>();

        if (chat.Type is TdApi.ChatType.ChatTypeSupergroup sg)
        {
            await CollectSupergroupMembersAsync(client, sg.SupergroupId, members, raw, ct);
        }
        else if (chat.Type is TdApi.ChatType.ChatTypeBasicGroup bg)
        {
            await CollectBasicGroupMembersAsync(client, bg.BasicGroupId, members, raw, ct);
        }
        else
        {
            _logger.Log("该聊天类型不支持导出成员 (仅超级群组和基本群组支持)");
            return;
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            string saveDir = TdlPaths.DefaultMembersDir;
            Directory.CreateDirectory(saveDir);
            outputPath = Path.Combine(saveDir, $"{chatId}_users.json");
        }

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        string json = JsonSerializer.Serialize(members, jsonOptions);

        string? dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllTextAsync(outputPath, json);
        _logger.Log($"已导出 {members.Count} 个成员");
        _logger.Log($"文件已保存到: {outputPath}");
    }

    async Task CollectSupergroupMembersAsync(
        TdClient client,
        long supergroupId,
        List<MemberInfo> members,
        bool raw,
        CancellationToken ct)
    {
        int offset = 0;
        int limit = 200;
        bool hasMore = true;

        while (hasMore)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var membersResult = await client.GetSupergroupMembersAsync(
                    supergroupId: (int)supergroupId,
                    filter: null,
                    offset: offset,
                    limit: limit);

                if (membersResult.Members == null || membersResult.Members.Length == 0)
                {
                    hasMore = false;
                    break;
                }

                foreach (var member in membersResult.Members)
                {
                    ct.ThrowIfCancellationRequested();
                    await TryAddMemberAsync(client, member, members, raw, ct);
                }

                if (membersResult.Members.Length < limit)
                {
                    hasMore = false;
                }
                else
                {
                    offset += limit;
                }

                _logger.Log($"已获取 {members.Count} 个成员...");
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
                _logger.Log($"获取成员时发生异常: {ex.Message}");
                hasMore = false;
            }
        }
    }

    async Task CollectBasicGroupMembersAsync(
        TdClient client,
        long basicGroupId,
        List<MemberInfo> members,
        bool raw,
        CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            var fullInfo = await client.GetBasicGroupFullInfoAsync((int)basicGroupId);

            if (fullInfo.Members == null || fullInfo.Members.Length == 0)
            {
                _logger.Log("该基本群组没有成员");
                return;
            }

            foreach (var member in fullInfo.Members)
            {
                ct.ThrowIfCancellationRequested();
                await TryAddMemberAsync(client, member, members, raw, ct);
            }

            _logger.Log($"已获取 {members.Count} 个成员...");
        }
        catch (TdException ex) when (ex.Error.Code == 429)
        {
            int retryAfter = ParseRetryAfter(ex);
            _logger.Log($"触发频率限制，等待 {retryAfter} 秒后继续...");
            await Task.Delay(retryAfter * 1000, ct);
        }
        catch (Exception ex)
        {
            _logger.Log($"获取成员时发生异常: {ex.Message}");
        }
    }

    async Task TryAddMemberAsync(
        TdClient client,
        TdApi.ChatMember member,
        List<MemberInfo> members,
        bool raw,
        CancellationToken ct)
    {
        try
        {
            long userId = ExtractMemberUserId(member);
            if (userId == 0)
            {
                return;
            }

            var user = await client.GetUserAsync(userId);
            members.Add(BuildMemberInfo(member, user, raw));
        }
        catch (Exception ex) { _logger.Log($"获取成员信息失败: {ex.Message}"); }
    }

    long ExtractMemberUserId(TdApi.ChatMember member)
    {
        if (member.MemberId is TdApi.MessageSender.MessageSenderUser senderUser)
        {
            return senderUser.UserId;
        }
        return 0;
    }

    MemberInfo BuildMemberInfo(TdApi.ChatMember member, TdApi.User user, bool raw)
    {
        var info = new MemberInfo
        {
            UserId = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Username = ExtractPrimaryUsername(user),
            PhoneNumber = user.PhoneNumber,
            Status = user.Status.GetType().Name.Replace("UserStatus", ""),
            MemberStatus = member.Status.GetType().Name.Replace("ChatMemberStatus", "")
        };

        if (raw)
        {
            info.RawData = new
            {
                User = user,
                Member = member
            };
        }

        return info;
    }

    string? ExtractPrimaryUsername(TdApi.User user)
    {
        if (user.Usernames?.ActiveUsernames == null || user.Usernames.ActiveUsernames.Length == 0)
        {
            return user.Usernames?.EditableUsername;
        }
        return user.Usernames.ActiveUsernames[0];
    }

}

public class ChannelExport
{
    public long ChatId { get; set; }
    public string ChatTitle { get; set; } = string.Empty;
    public DateTime ExportTime { get; set; }
    public int TotalMessages { get; set; }
    public List<MessageGroup> Groups { get; set; } = [];
}

public class MessageGroup
{
    public string? MediaAlbumId { get; set; }
    public bool IsGrouped { get; set; }
    public List<MessageInfo> Messages { get; set; } = [];
}

public class MessageInfo
{
    public long MessageId { get; set; }
    public DateTime Date { get; set; }
    public DateTime? EditDate { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Text { get; set; }
    public MediaInfo? Media { get; set; }
    public ForwardInfoExport? ForwardInfo { get; set; }
    public List<MessageInfo>? Comments { get; set; }
}

public class MediaInfo
{
    public string Type { get; set; } = string.Empty;
    public string? FileId { get; set; }
    public string? FileName { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int? Duration { get; set; }
    public string? MimeType { get; set; }
    public long? FileSize { get; set; }
}

public class ForwardInfoExport
{
    public long FromChatId { get; set; }
    public long FromMessageId { get; set; }
    public DateTime? Date { get; set; }
    public string? Origin { get; set; }
}

public class ChatInfo
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public LastMessageInfo? LastMessage { get; set; }
}

public class LastMessageInfo
{
    public long Id { get; set; }
    public DateTime Date { get; set; }
    public string Type { get; set; } = string.Empty;
}
public class MemberInfo
{
    public long UserId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Username { get; set; }
    public string? PhoneNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public string MemberStatus { get; set; } = string.Empty;
    public object? RawData { get; set; }
}
