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

    private TimeSpan _tokenRefreshSkew = TimeSpan.FromMinutes(5);

    /// <summary>
    /// AccessToken 提前刷新的提前量。默认 5 分钟。
    /// 实际过期时间 - 此提前量 = 缓存有效期，避免在临近过期时使用。
    /// 若该值大于等于 token 实际有效期，会被动态 clamp 到 有效期/2，保证仍有缓存。
    /// 不能为负数。
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">赋值为负数时抛出</exception>
    public TimeSpan TokenRefreshSkew
    {
        get => _tokenRefreshSkew;
        set => _tokenRefreshSkew = value >= TimeSpan.Zero
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "TokenRefreshSkew 不能为负数");
    }
}
