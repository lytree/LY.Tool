using System.Text.Encodings.Web;
using System.Text.Json;
using TdLib;

namespace Avalonia.Plugin.TDLSharp.Services;

public partial class TdlService
{
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
            string saveDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "tdl", "members");
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
