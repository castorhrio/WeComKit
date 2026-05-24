using System.Text.Json.Serialization;

namespace WeComKit.Api.Models;

/// <summary>
/// 企业微信 OAuth 用户身份响应。
/// </summary>
public class WeComOAuthUserInfoResponse : WeComApiResult
{
    [JsonPropertyName("UserId")]
    public string? UserId { get; set; }

    [JsonPropertyName("DeviceId")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("user_ticket")]
    public string? UserTicket { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("OpenId")]
    public string? OpenId { get; set; }

    [JsonPropertyName("external_userid")]
    public string? ExternalUserId { get; set; }
}

/// <summary>
/// 企业微信 OAuth 敏感用户信息响应。
/// </summary>
public class WeComUserDetailResponse : WeComApiResult
{
    [JsonPropertyName("userid")]
    public string? UserId { get; set; }

    [JsonPropertyName("gender")]
    public string? Gender { get; set; }

    [JsonPropertyName("avatar")]
    public string? Avatar { get; set; }

    [JsonPropertyName("qr_code")]
    public string? QrCode { get; set; }

    [JsonPropertyName("mobile")]
    public string? Mobile { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("biz_mail")]
    public string? BizMail { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }
}

internal sealed class WeComUserDetailRequest
{
    [JsonPropertyName("user_ticket")]
    public string UserTicket { get; set; } = string.Empty;
}
