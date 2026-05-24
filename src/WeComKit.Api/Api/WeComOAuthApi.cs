using WeComKit.Api.Http;
using WeComKit.Api.Models;

namespace WeComKit.Api;

/// <summary>
/// 企业微信网页授权 API。
/// </summary>
public class WeComOAuthApi
{
    private const string DefaultAuthorizeUrl = "https://open.weixin.qq.com/connect/oauth2/authorize";

    private readonly WeComHttpClient _client;
    private readonly WeComOptions _options;

    public WeComOAuthApi(WeComHttpClient client, WeComOptions options)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// 构建企业微信网页授权地址。
    /// </summary>
    public string BuildAuthorizeUrl(string redirectUri, string? state = null, string scope = "snsapi_base")
    {
        if (string.IsNullOrWhiteSpace(redirectUri))
            throw new ArgumentException("回调地址不能为空", nameof(redirectUri));
        if (string.IsNullOrWhiteSpace(scope))
            throw new ArgumentException("授权 scope 不能为空", nameof(scope));

        var query = new List<string>
        {
            $"appid={Uri.EscapeDataString(_options.CorpId)}",
            $"redirect_uri={Uri.EscapeDataString(redirectUri)}",
            "response_type=code",
            $"scope={Uri.EscapeDataString(scope)}",
            $"state={Uri.EscapeDataString(state ?? string.Empty)}"
        };

        if (!string.IsNullOrWhiteSpace(_options.AgentId))
            query.Add($"agentid={Uri.EscapeDataString(_options.AgentId)}");

        return $"{DefaultAuthorizeUrl}?{string.Join("&", query)}#wechat_redirect";
    }

    /// <summary>
    /// 通过 OAuth code 获取成员身份。
    /// </summary>
    public Task<WeComOAuthUserInfoResponse> GetUserInfoAsync(string code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("OAuth code 不能为空", nameof(code));

        return _client.GetAsync<WeComOAuthUserInfoResponse>("/cgi-bin/auth/getuserinfo", new Dictionary<string, string>
        {
            ["code"] = code
        }, ct);
    }

    /// <summary>
    /// 通过 user_ticket 获取成员敏感信息。
    /// </summary>
    public Task<WeComUserDetailResponse> GetUserDetailAsync(string userTicket, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userTicket))
            throw new ArgumentException("user_ticket 不能为空", nameof(userTicket));

        return _client.PostAsync<WeComUserDetailResponse>("/cgi-bin/auth/getuserdetail", new WeComUserDetailRequest
        {
            UserTicket = userTicket
        }, ct);
    }
}
