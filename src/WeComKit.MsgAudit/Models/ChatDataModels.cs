using System.Text.Json.Serialization;

namespace WeComKit.MsgAudit.Models;

/// <summary>
/// GetChatData 返回的原始 JSON 结构（解密前）
/// </summary>
public class ChatDataResponse
{
    [JsonPropertyName("errcode")]
    public int ErrCode { get; set; }

    [JsonPropertyName("errmsg")]
    public string ErrMsg { get; set; } = string.Empty;

    [JsonPropertyName("chatdata")]
    public List<ChatDataItem> ChatData { get; set; } = new();
}

/// <summary>
/// 单条加密的会话消息
/// </summary>
public class ChatDataItem
{
    [JsonPropertyName("seq")]
    public long Seq { get; set; }

    [JsonPropertyName("msgid")]
    public string MsgId { get; set; } = string.Empty;

    [JsonPropertyName("publickey_ver")]
    public int PublicKeyVer { get; set; }

    [JsonPropertyName("encrypt_random_key")]
    public string EncryptRandomKey { get; set; } = string.Empty;

    [JsonPropertyName("encrypt_chat_msg")]
    public string EncryptChatMsg { get; set; } = string.Empty;
}

/// <summary>
/// 解密后的单条会话消息
/// 映射 WeCom 消息体 JSON 的通用字段，类型相关字段按需反序列化
/// </summary>
public class ChatRecord
{
    [JsonPropertyName("msgid")]
    public string MsgId { get; set; } = string.Empty;

    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    [JsonPropertyName("tolist")]
    public string[] ToList { get; set; } = Array.Empty<string>();

    [JsonPropertyName("roomid")]
    public string RoomId { get; set; } = string.Empty;

    [JsonPropertyName("msgtime")]
    public long MsgTime { get; set; }

    [JsonPropertyName("msgtype")]
    public string MsgType { get; set; } = string.Empty;

    // ---- 常见消息类型的字段 ----

    [JsonPropertyName("text")]
    public ChatText? Text { get; set; }

    [JsonPropertyName("image")]
    public ChatMedia? Image { get; set; }

    [JsonPropertyName("voice")]
    public ChatMedia? Voice { get; set; }

    [JsonPropertyName("video")]
    public ChatMedia? Video { get; set; }

    [JsonPropertyName("file")]
    public ChatFile? File { get; set; }

    [JsonPropertyName("link")]
    public ChatLink? Link { get; set; }

    [JsonPropertyName("weapp")]
    public ChatWeApp? WeApp { get; set; }

    [JsonPropertyName("emotion")]
    public ChatMedia? Emotion { get; set; }
}

public class ChatText
{
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public class ChatMedia
{
    [JsonPropertyName("sdkfileid")]
    public string SdkFileId { get; set; } = string.Empty;

    [JsonPropertyName("md5sum")]
    public string Md5Sum { get; set; } = string.Empty;

    [JsonPropertyName("filesize")]
    public long FileSize { get; set; }

    [JsonPropertyName("play_length")]
    public int PlayLength { get; set; }
}

public class ChatFile
{
    [JsonPropertyName("sdkfileid")]
    public string SdkFileId { get; set; } = string.Empty;

    [JsonPropertyName("md5sum")]
    public string Md5Sum { get; set; } = string.Empty;

    [JsonPropertyName("filename")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("fileext")]
    public string FileExt { get; set; } = string.Empty;

    [JsonPropertyName("filesize")]
    public long FileSize { get; set; }
}

public class ChatLink
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("link_url")]
    public string LinkUrl { get; set; } = string.Empty;

    [JsonPropertyName("image_url")]
    public string ImageUrl { get; set; } = string.Empty;
}

public class ChatWeApp
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string UserName { get; set; } = string.Empty;

    [JsonPropertyName("displayname")]
    public string DisplayName { get; set; } = string.Empty;
}
