using TdLib;

namespace LYBox.Plugin.TDLSharp.Services;

/// <summary>
/// 统一处理 <see cref="TdApi.MessageContent"/> 的子类型分发：文本提取、媒体文件抽取、
/// 人类可读类型名。以前这些 switch 散落在 TdlService.Clear / TdlService.Export / TdlService.cs 中。
/// </summary>
public static class MessageContentInspector
{
    /// <summary>返回该消息内容的简短类型名（如 "Text"、"Photo"），无匹配则返回去掉 "Message" 前缀的类型名。</summary>
    public static string GetTypeName(TdApi.MessageContent content) => content switch
    {
        TdApi.MessageContent.MessageText => "Text",
        TdApi.MessageContent.MessagePhoto => "Photo",
        TdApi.MessageContent.MessageVideo => "Video",
        TdApi.MessageContent.MessageAudio => "Audio",
        TdApi.MessageContent.MessageDocument => "Document",
        TdApi.MessageContent.MessageVoiceNote => "VoiceNote",
        TdApi.MessageContent.MessageVideoNote => "VideoNote",
        TdApi.MessageContent.MessageSticker => "Sticker",
        TdApi.MessageContent.MessageAnimation => "Animation",
        TdApi.MessageContent.MessageContact => "Contact",
        TdApi.MessageContent.MessageLocation => "Location",
        TdApi.MessageContent.MessageVenue => "Venue",
        TdApi.MessageContent.MessagePoll => "Poll",
        TdApi.MessageContent.MessageDice => "Dice",
        TdApi.MessageContent.MessageGame => "Game",
        TdApi.MessageContent.MessageInvoice => "Invoice",
        TdApi.MessageContent.MessageCall => "Call",
        TdApi.MessageContent.MessagePinMessage => "PinMessage",
        TdApi.MessageContent.MessageStory => "Story",
        TdApi.MessageContent.MessageUnsupported => "Unsupported",
        _ => content.GetType().Name.Replace("Message", string.Empty),
    };

    /// <summary>提取消息中的人类可读文本（caption / body）；不支持的消息类型返回 null。</summary>
    public static string? GetText(TdApi.MessageContent content) => content switch
    {
        TdApi.MessageContent.MessageText t => t.Text?.Text,
        TdApi.MessageContent.MessagePhoto p => p.Caption?.Text,
        TdApi.MessageContent.MessageVideo v => v.Caption?.Text,
        TdApi.MessageContent.MessageAudio a => a.Caption?.Text,
        TdApi.MessageContent.MessageDocument d => d.Caption?.Text,
        TdApi.MessageContent.MessageVoiceNote vn => vn.Caption?.Text,
        TdApi.MessageContent.MessageAnimation ani => ani.Caption?.Text,
        TdApi.MessageContent.MessagePinMessage pm => $"[PinMessage] MsgId={pm.MessageId}",
        TdApi.MessageContent.MessageUnsupported => "This channel can't be displayed",
        _ => null,
    };

    /// <summary>提取消息中可下载的 <see cref="TdApi.File"/>；非媒体类型返回 null。</summary>
    public static TdApi.File? GetDownloadableFile(TdApi.MessageContent content) => content switch
    {
        TdApi.MessageContent.MessagePhoto p => p.Photo.Sizes.LastOrDefault()?.Photo,
        TdApi.MessageContent.MessageVideo v => v.Video.Video_,
        TdApi.MessageContent.MessageAudio a => a.Audio.Audio_,
        TdApi.MessageContent.MessageDocument d => d.Document.Document_,
        TdApi.MessageContent.MessageVoiceNote vn => vn.VoiceNote.Voice,
        TdApi.MessageContent.MessageVideoNote vn => vn.VideoNote.Video,
        TdApi.MessageContent.MessageAnimation ani => ani.Animation.Animation_,
        TdApi.MessageContent.MessageSticker s => s.Sticker.Sticker_,
        _ => null,
    };

    /// <summary>提取消息中可下载的原始文件名（用于下载场景）。空时回退为 <c>file_{fileId}</c>。</summary>
    public static string GetFileName(TdApi.MessageContent content, TdApi.File file) => content switch
    {
        TdApi.MessageContent.MessageVideo v => v.Video.FileName,
        TdApi.MessageContent.MessageAudio a => a.Audio.FileName,
        TdApi.MessageContent.MessageDocument d => d.Document.FileName,
        TdApi.MessageContent.MessageAnimation ani => ani.Animation.FileName,
        TdApi.MessageContent.MessageSticker s => $"{s.Sticker.SetId}_{s.Sticker.Sticker_.Id}.webp",
        _ => $"file_{file.Id}",
    };
}
