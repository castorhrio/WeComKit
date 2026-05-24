using System.Text.Json.Serialization;

namespace WeComKit.Api.Models;

/// <summary>
/// 第三方应用 suite_access_token 响应。
/// </summary>
public class WeComSuiteTokenResponse : WeComApiResult
{
    [JsonPropertyName("suite_access_token")]
    public string SuiteAccessToken { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}

/// <summary>
/// 预授权码响应。
/// </summary>
public class WeComPreAuthCodeResponse : WeComApiResult
{
    [JsonPropertyName("pre_auth_code")]
    public string PreAuthCode { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}

/// <summary>
/// 永久授权码响应。
/// </summary>
public class WeComPermanentCodeResponse : WeComApiResult
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("permanent_code")]
    public string PermanentCode { get; set; } = string.Empty;

    [JsonPropertyName("auth_corp_info")]
    public WeComAuthCorpInfo? AuthCorpInfo { get; set; }

    [JsonPropertyName("auth_user_info")]
    public WeComAuthUserInfo? AuthUserInfo { get; set; }
}

/// <summary>
/// 授权企业 token 响应。
/// </summary>
public class WeComCorpTokenResponse : WeComApiResult
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}

public class WeComAuthCorpInfo
{
    [JsonPropertyName("corpid")]
    public string CorpId { get; set; } = string.Empty;

    [JsonPropertyName("corp_name")]
    public string? CorpName { get; set; }

    [JsonPropertyName("corp_type")]
    public string? CorpType { get; set; }

    [JsonPropertyName("corp_square_logo_url")]
    public string? CorpSquareLogoUrl { get; set; }
}

public class WeComAuthUserInfo
{
    [JsonPropertyName("userid")]
    public string? UserId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("avatar")]
    public string? Avatar { get; set; }
}

internal sealed class WeComSuiteTokenRequest
{
    [JsonPropertyName("suite_id")]
    public string SuiteId { get; set; } = string.Empty;

    [JsonPropertyName("suite_secret")]
    public string SuiteSecret { get; set; } = string.Empty;

    [JsonPropertyName("suite_ticket")]
    public string SuiteTicket { get; set; } = string.Empty;
}

internal sealed class WeComPermanentCodeRequest
{
    [JsonPropertyName("auth_code")]
    public string AuthCode { get; set; } = string.Empty;
}

internal sealed class WeComCorpTokenRequest
{
    [JsonPropertyName("auth_corpid")]
    public string AuthCorpId { get; set; } = string.Empty;

    [JsonPropertyName("permanent_code")]
    public string PermanentCode { get; set; } = string.Empty;
}
