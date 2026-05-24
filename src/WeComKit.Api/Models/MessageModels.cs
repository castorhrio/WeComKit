using System.Text.Json.Serialization;

namespace WeComKit.Api.Models;

#region 消息类型

/// <summary>
/// 发送消息请求基类
/// </summary>
public class MessageSendRequest
{
    /// <summary>成员 ID 列表（touser / toparty / totag 三选一，不可全空）</summary>
    [JsonPropertyName("touser")]
    public string? ToUser { get; set; }

    /// <summary>部门 ID 列表</summary>
    [JsonPropertyName("toparty")]
    public string? ToParty { get; set; }

    /// <summary>标签 ID 列表</summary>
    [JsonPropertyName("totag")]
    public string? ToTag { get; set; }

    /// <summary>消息类型</summary>
    [JsonPropertyName("msgtype")]
    public string MsgType { get; set; } = string.Empty;

    /// <summary>应用 AgentId</summary>
    [JsonPropertyName("agentid")]
    public int? AgentId { get; set; }

    /// <summary>是否开启安全（1 表示保密消息）</summary>
    [JsonPropertyName("safe")]
    public int? Safe { get; set; }

    /// <summary>是否开启重复消息检查</summary>
    [JsonPropertyName("enable_duplicate_check")]
    public int? EnableDuplicateCheck { get; set; }

    /// <summary>重复消息检查间隔（秒，最大 4 小时）</summary>
    [JsonPropertyName("duplicate_check_interval")]
    public int? DuplicateCheckInterval { get; set; }
}

/// <summary>
/// 文本消息体
/// </summary>
public class TextMessage
{
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// 图片/语音/文件消息体
/// </summary>
public class MediaMessage
{
    [JsonPropertyName("media_id")]
    public string MediaId { get; set; } = string.Empty;
}

/// <summary>
/// 图片消息（带图片描述）
/// </summary>
public class ImageMessage : MediaMessage
{
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

/// <summary>
/// 视频消息体
/// </summary>
public class VideoMessage
{
    [JsonPropertyName("media_id")]
    public string MediaId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

/// <summary>
/// 文本卡片消息体
/// </summary>
public class TextCardMessage
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("btntxt")]
    public string? BtnTxt { get; set; }
}

/// <summary>
/// 图文消息体
/// </summary>
public class NewsMessage
{
    [JsonPropertyName("articles")]
    public List<NewsArticle> Articles { get; set; } = new();
}

/// <summary>
/// 图文消息单篇文章
/// </summary>
public class NewsArticle
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("picurl")]
    public string? PicUrl { get; set; }
}

/// <summary>
/// Markdown 消息体
/// </summary>
public class MarkdownMessage
{
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

#endregion

#region 消息发送请求（按类型区分）

/// <summary>
/// 文本消息请求
/// </summary>
public class TextMessageRequest : MessageSendRequest
{
    public TextMessageRequest()
    {
        MsgType = "text";
    }

    [JsonPropertyName("text")]
    public TextMessage Text { get; set; } = new();
}

/// <summary>
/// 图片消息请求
/// </summary>
public class ImageMessageRequest : MessageSendRequest
{
    public ImageMessageRequest()
    {
        MsgType = "image";
    }

    [JsonPropertyName("image")]
    public ImageMessage Image { get; set; } = new();
}

/// <summary>
/// 语音消息请求
/// </summary>
public class VoiceMessageRequest : MessageSendRequest
{
    public VoiceMessageRequest()
    {
        MsgType = "voice";
    }

    [JsonPropertyName("voice")]
    public MediaMessage Voice { get; set; } = new();
}

/// <summary>
/// 视频消息请求
/// </summary>
public class VideoMessageRequest : MessageSendRequest
{
    public VideoMessageRequest()
    {
        MsgType = "video";
    }

    [JsonPropertyName("video")]
    public VideoMessage Video { get; set; } = new();

    [JsonPropertyName("enable_id_trans")]
    public int? EnableIdTrans { get; set; }
}

/// <summary>
/// 文件消息请求
/// </summary>
public class FileMessageRequest : MessageSendRequest
{
    public FileMessageRequest()
    {
        MsgType = "file";
    }

    [JsonPropertyName("file")]
    public MediaMessage File { get; set; } = new();
}

/// <summary>
/// 文本卡片消息请求
/// </summary>
public class TextCardMessageRequest : MessageSendRequest
{
    public TextCardMessageRequest()
    {
        MsgType = "textcard";
    }

    [JsonPropertyName("textcard")]
    public TextCardMessage TextCard { get; set; } = new();
}

/// <summary>
/// 图文消息请求
/// </summary>
public class NewsMessageRequest : MessageSendRequest
{
    public NewsMessageRequest()
    {
        MsgType = "news";
    }

    [JsonPropertyName("news")]
    public NewsMessage News { get; set; } = new();
}

/// <summary>
/// Markdown 消息请求
/// </summary>
public class MarkdownMessageRequest : MessageSendRequest
{
    public MarkdownMessageRequest()
    {
        MsgType = "markdown";
    }

    [JsonPropertyName("markdown")]
    public MarkdownMessage Markdown { get; set; } = new();
}

/// <summary>
/// 模板卡片消息请求。
/// </summary>
public class TemplateCardMessageRequest : MessageSendRequest
{
    public TemplateCardMessageRequest()
    {
        MsgType = "template_card";
    }

    /// <summary>模板卡片 JSON 对象。不同模板卡片类型字段差异较大，调用方按企业微信官方格式传入。</summary>
    [JsonPropertyName("template_card")]
    public object TemplateCard { get; set; } = new();
}

/// <summary>
/// 消息发送响应
/// </summary>
public class MessageSendResponse : WeComApiResult
{
    [JsonPropertyName("invaliduser")]
    public string InvalidUser { get; set; } = string.Empty;

    [JsonPropertyName("invalidparty")]
    public string InvalidParty { get; set; } = string.Empty;

    [JsonPropertyName("invalidtag")]
    public string InvalidTag { get; set; } = string.Empty;

    [JsonPropertyName("msgid")]
    public string? MsgId { get; set; }
}

#endregion
