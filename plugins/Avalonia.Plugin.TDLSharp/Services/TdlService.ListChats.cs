using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using TdLib;

namespace Avalonia.Plugin.TDLSharp.Services;

public partial class TdlService
{
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
            string saveDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "tdl", "chats");
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
            Type = GetMessageType(msg.Content)
        };
    }
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
