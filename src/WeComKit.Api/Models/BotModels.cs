using System.Text.Json.Serialization;

namespace WeComKit.Api.Models;

/// <summary>
/// 企业微信机器人消息响应。
/// </summary>
public class WeComBotMessageResponse : WeComApiResult
{
}

/// <summary>
/// 机器人文本消息。
/// </summary>
public class WeComBotTextMessage
{
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("mentioned_list")]
    public List<string>? MentionedList { get; set; }

    [JsonPropertyName("mentioned_mobile_list")]
    public List<string>? MentionedMobileList { get; set; }
}

/// <summary>
/// 机器人 Markdown 消息。
/// </summary>
public class WeComBotMarkdownMessage
{
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

internal sealed class WeComBotTextMessageRequest
{
    [JsonPropertyName("msgtype")]
    public string MsgType { get; set; } = "text";

    [JsonPropertyName("text")]
    public WeComBotTextMessage Text { get; set; } = new();
}

internal sealed class WeComBotMarkdownMessageRequest
{
    [JsonPropertyName("msgtype")]
    public string MsgType { get; set; } = "markdown";

    [JsonPropertyName("markdown")]
    public WeComBotMarkdownMessage Markdown { get; set; } = new();
}
