using System.Net.Http.Json;
using WeComKit.Api.Http;
using WeComKit.Api.Models;

namespace WeComKit.Api;

/// <summary>
/// 企业微信第三方应用 suite 授权 API。
/// </summary>
public class WeComSuiteApi
{
    private readonly HttpClient _http;
    private readonly WeComSuiteOptions _options;

    public WeComSuiteApi(HttpClient httpClient, WeComSuiteOptions options)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(options.ApiUrl))
            throw new ArgumentException("ApiUrl 不能为空", nameof(options));
        if (string.IsNullOrWhiteSpace(options.SuiteId))
            throw new ArgumentException("SuiteId 不能为空", nameof(options));
        if (string.IsNullOrWhiteSpace(options.SuiteSecret))
            throw new ArgumentException("SuiteSecret 不能为空", nameof(options));
    }

    /// <summary>
    /// 获取 suite_access_token。suite_ticket 应由应用层从回调事件中保存。
    /// </summary>
    public Task<WeComSuiteTokenResponse> GetSuiteTokenAsync(string suiteTicket, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(suiteTicket))
            throw new ArgumentException("suite_ticket 不能为空", nameof(suiteTicket));

        return PostAsync<WeComSuiteTokenResponse>("/cgi-bin/service/get_suite_token", new WeComSuiteTokenRequest
        {
            SuiteId = _options.SuiteId,
            SuiteSecret = _options.SuiteSecret,
            SuiteTicket = suiteTicket
        }, ct);
    }

    /// <summary>
    /// 获取预授权码。
    /// </summary>
    public Task<WeComPreAuthCodeResponse> GetPreAuthCodeAsync(string suiteAccessToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(suiteAccessToken))
            throw new ArgumentException("suite_access_token 不能为空", nameof(suiteAccessToken));

        var path = $"/cgi-bin/service/get_pre_auth_code?suite_access_token={Uri.EscapeDataString(suiteAccessToken)}";
        return GetAsync<WeComPreAuthCodeResponse>(path, ct);
    }

    /// <summary>
    /// 使用临时授权码换取永久授权码。
    /// </summary>
    public Task<WeComPermanentCodeResponse> GetPermanentCodeAsync(string suiteAccessToken, string authCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(suiteAccessToken))
            throw new ArgumentException("suite_access_token 不能为空", nameof(suiteAccessToken));
        if (string.IsNullOrWhiteSpace(authCode))
            throw new ArgumentException("auth_code 不能为空", nameof(authCode));

        var path = $"/cgi-bin/service/get_permanent_code?suite_access_token={Uri.EscapeDataString(suiteAccessToken)}";
        return PostAsync<WeComPermanentCodeResponse>(path, new WeComPermanentCodeRequest
        {
            AuthCode = authCode
        }, ct);
    }

    /// <summary>
    /// 使用永久授权码获取授权企业 access_token。
    /// </summary>
    public Task<WeComCorpTokenResponse> GetCorpTokenAsync(
        string suiteAccessToken,
        string authCorpId,
        string permanentCode,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(suiteAccessToken))
            throw new ArgumentException("suite_access_token 不能为空", nameof(suiteAccessToken));
        if (string.IsNullOrWhiteSpace(authCorpId))
            throw new ArgumentException("auth_corpid 不能为空", nameof(authCorpId));
        if (string.IsNullOrWhiteSpace(permanentCode))
            throw new ArgumentException("permanent_code 不能为空", nameof(permanentCode));

        var path = $"/cgi-bin/service/get_corp_token?suite_access_token={Uri.EscapeDataString(suiteAccessToken)}";
        return PostAsync<WeComCorpTokenResponse>(path, new WeComCorpTokenRequest
        {
            AuthCorpId = authCorpId,
            PermanentCode = permanentCode
        }, ct);
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken ct)
        where T : WeComApiResult
    {
        var url = BuildUrl(path);
        using var response = await _http.GetAsync(url, ct);
        return await WeComHttpClient.ReadApiResultAsync<T>(response, $"GET {path}", url, ct);
    }

    private async Task<T> PostAsync<T>(string path, object body, CancellationToken ct)
        where T : WeComApiResult
    {
        var url = BuildUrl(path);
        using var response = await _http.PostAsJsonAsync(url, body, WeComHttpClient.JsonOptions, ct);
        return await WeComHttpClient.ReadApiResultAsync<T>(response, $"POST {path}", url, ct);
    }

    private string BuildUrl(string path)
    {
        return $"{_options.ApiUrl.TrimEnd('/')}{path}";
    }
}
