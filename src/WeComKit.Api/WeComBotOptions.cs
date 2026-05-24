namespace WeComKit.Api;

/// <summary>
/// 企业微信群机器人配置。
/// </summary>
public class WeComBotOptions
{
    /// <summary>群机器人 webhook 基础地址。</summary>
    public string WebhookUrl { get; set; } = "https://qyapi.weixin.qq.com/cgi-bin/webhook/send";

    /// <summary>群机器人 key。</summary>
    public string WebhookKey { get; set; } = string.Empty;
}
