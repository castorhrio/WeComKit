namespace WeComKit.MsgAudit;

/// <summary>
/// 企业微信会话存档配置。
/// </summary>
public class WeComMsgAuditOptions
{
    /// <summary>企业 ID。</summary>
    public string CorpId { get; set; } = string.Empty;

    /// <summary>会话存档 Secret。</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>RSA 私钥（PEM 格式）。</summary>
    public string PrivateKey { get; set; } = string.Empty;

    /// <summary>企业微信官方会话存档 SDK 原生文件目录。</summary>
    public string? SdkDirectory { get; set; }
}
