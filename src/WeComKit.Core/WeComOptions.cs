namespace WeComKit.Core;

/// <summary>
/// 微信公众号配置
/// </summary>
public class WeChatMPOptions
{
    /// <summary>回调 Token</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>消息加解密密钥（43 位 Base64）</summary>
    public string EncodingAESKey { get; set; } = string.Empty;

    /// <summary>AppId</summary>
    public string AppId { get; set; } = string.Empty;
}
