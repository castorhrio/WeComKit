namespace WeComKit.Api;

/// <summary>
/// 企业微信自建应用配置。
/// </summary>
public class WeComOptions
{
    /// <summary>API 基础地址，默认 https://qyapi.weixin.qq.com。</summary>
    public string ApiUrl { get; set; } = "https://qyapi.weixin.qq.com";

    /// <summary>企业 ID。</summary>
    public string CorpId { get; set; } = string.Empty;

    /// <summary>应用 AgentId。</summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>应用 Secret。</summary>
    public string Secret { get; set; } = string.Empty;
}
