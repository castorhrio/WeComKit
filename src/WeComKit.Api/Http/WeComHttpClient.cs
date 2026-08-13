using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using WeComKit.Api.Models;

namespace WeComKit.Api.Http;

/// <summary>
/// 企业微信 API HTTP 客户端，内置 AccessToken 自动获取、缓存和刷新
/// </summary>
/// <remarks>
/// Retry 策略：
/// <list type="bullet">
/// <item><b>网络失败（超时 / 连接错误 / 5xx / 非 WeCom 业务错误）不自动重试。</b>
/// 网络超时不等于服务端未执行；对 SendMessage / Create 等非幂等 API 而言，
/// 自动重试可能导致重复副作用（如重复发送消息）。</item>
/// <item><b>AccessToken 错误（40001 / 40014 / 42001）会触发一次重试：</b>
/// <see cref="GetAsync{T}"/> / <see cref="PostAsync{T}"/> 在收到此类业务错误后失效缓存并刷新 Token，
/// 然后用新 Token 重发<b>一次</b>（POST 同样会重发，调用方需知悉此可能的重复副作用）。
/// 可定位（seekable）的 <see cref="PostMultipartAsync{T}"/> 流亦会重发一次。</item>
/// <item>除上述 Token 错误恢复外，不提供其他自动重试。</item>
/// </list>
/// 需要更复杂的重试时由调用方在幂等接口上自行实现，并配合 CancellationToken。
/// </remarks>
public class WeComHttpClient : IDisposable
{
    private static readonly HashSet<int> AccessTokenErrorCodes = new()
    {
        40001, // invalid credential
        40014, // invalid access_token
        42001  // access_token expired
    };

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly WeComOptions _options;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    private string? _token;
    private DateTime _tokenExpiry = DateTime.MinValue;

    /// <summary>
    /// 初始化 HTTP 客户端
    /// </summary>
    /// <param name="httpClient">HttpClient 实例（推荐使用 IHttpClientFactory 创建）</param>
    /// <param name="options">企业微信配置</param>
    public WeComHttpClient(HttpClient httpClient, WeComOptions options)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(options.ApiUrl))
            throw new ArgumentException("ApiUrl 不能为空", nameof(options));
        if (string.IsNullOrWhiteSpace(options.CorpId))
            throw new ArgumentException("CorpId 不能为空", nameof(options));
        if (string.IsNullOrWhiteSpace(options.Secret))
            throw new ArgumentException("Secret 不能为空", nameof(options));
    }

    /// <summary>
    /// 获取有效的 AccessToken（自动缓存和刷新）
    /// </summary>
    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        if (_token != null && DateTime.UtcNow < _tokenExpiry)
            return _token;

        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_token != null && DateTime.UtcNow < _tokenExpiry)
                return _token;

            var url = BuildUrl("/cgi-bin/gettoken", new Dictionary<string, string>
            {
                ["corpid"] = _options.CorpId,
                ["corpsecret"] = _options.Secret
            });

            using var response = await _http.GetAsync(url, ct);

            // 自定义 HttpMessageHandler 可能返回不带 RequestMessage 的响应，
            // 此时用已构造的 url 兜底，保证 RequestPath 始终可还原（经脱敏后写入异常）。
            var path = response.RequestMessage?.RequestUri?.ToString() ?? url;

            if (!response.IsSuccessStatusCode)
            {
                throw new WeComApiException(
                    -1,
                    $"获取 AccessToken HTTP 失败：{(int)response.StatusCode}",
                    path,
                    response.StatusCode);
            }

            WeComTokenResponse? result;
            try
            {
                result = await response.Content.ReadFromJsonAsync<WeComTokenResponse>(cancellationToken: ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new WeComApiException(
                    -1,
                    $"获取 AccessToken 响应反序列化失败：{ex.Message}",
                    path,
                    response.StatusCode);
            }

            if (result is null)
            {
                throw new WeComApiException(
                    -1,
                    "获取 AccessToken 响应为空",
                    path,
                    response.StatusCode);
            }

            if (result.ErrCode != 0)
            {
                throw new WeComApiException(
                    result.ErrCode,
                    result.ErrMsg,
                    path,
                    response.StatusCode);
            }

            // 提前刷新：实际过期时间 - TokenRefreshSkew。
            // 若服务端返回的 ExpiresIn 非法（<=0），不写缓存，下一次调用重新获取。
            // 若配置的 skew >= token 实际有效期，动态 clamp 到 有效期/2，保证仍能短期缓存，
            // 维持单实例并发下的 single-flight（避免排队打爆 token 接口）。
            if (result.ExpiresIn <= 0)
            {
                _token = null;
                _tokenExpiry = DateTime.MinValue;
                return result.AccessToken;
            }

            var lifetime = TimeSpan.FromSeconds(result.ExpiresIn);
            var effectiveSkew = _options.TokenRefreshSkew < lifetime
                ? _options.TokenRefreshSkew
                : lifetime / 2;
            _token = result.AccessToken;
            _tokenExpiry = DateTime.UtcNow + lifetime - effectiveSkew;
            return _token;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    /// <summary>
    /// 发送 GET 请求并反序列化响应
    /// </summary>
    /// <typeparam name="T">响应类型（必须继承 WeComApiResult）</typeparam>
    /// <param name="path">API 路径，如 /cgi-bin/department/list</param>
    /// <param name="queryParams">额外的查询参数（不含 access_token）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>反序列化后的响应对象</returns>
    public async Task<T> GetAsync<T>(string path, IDictionary<string, string>? queryParams = null, CancellationToken ct = default)
        where T : WeComApiResult
    {
        var token = await GetAccessTokenAsync(ct);
        try
        {
            return await GetWithAccessTokenAsync<T>(path, token, queryParams, ct);
        }
        catch (WeComApiException ex) when (IsAccessTokenError(ex))
        {
            InvalidateToken();
            token = await GetAccessTokenAsync(ct);
            return await GetWithAccessTokenAsync<T>(path, token, queryParams, ct);
        }
    }

    /// <summary>
    /// 使用指定 AccessToken 发送 GET 请求。
    /// </summary>
    public async Task<T> GetWithAccessTokenAsync<T>(string path, string accessToken, IDictionary<string, string>? queryParams = null, CancellationToken ct = default)
        where T : WeComApiResult
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new ArgumentException("AccessToken 不能为空", nameof(accessToken));

        var url = BuildUrl(path, queryParams, accessToken);
        using var response = await _http.GetAsync(url, ct);
        return await ReadApiResultAsync<T>(response, $"GET {path}", url, ct);
    }

    /// <summary>
    /// 发送 POST 请求（JSON body）并反序列化响应
    /// </summary>
    public async Task<T> PostAsync<T>(string path, object? body, CancellationToken ct = default)
        where T : WeComApiResult
    {
        var token = await GetAccessTokenAsync(ct);
        try
        {
            return await PostWithAccessTokenAsync<T>(path, token, body, ct);
        }
        catch (WeComApiException ex) when (IsAccessTokenError(ex))
        {
            InvalidateToken();
            token = await GetAccessTokenAsync(ct);
            return await PostWithAccessTokenAsync<T>(path, token, body, ct);
        }
    }

    /// <summary>
    /// 使用指定 AccessToken 发送 POST 请求（JSON body）。
    /// </summary>
    public async Task<T> PostWithAccessTokenAsync<T>(string path, string accessToken, object? body, CancellationToken ct = default)
        where T : WeComApiResult
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new ArgumentException("AccessToken 不能为空", nameof(accessToken));

        var url = BuildUrl(path, null, accessToken);

        using var response = body is null
            ? await _http.PostAsync(url, null, ct)
            : await _http.PostAsJsonAsync(url, body, JsonOptions, ct);

        return await ReadApiResultAsync<T>(response, $"POST {path}", url, ct);
    }

    /// <summary>
    /// 发送不携带 AccessToken 的 POST 请求（用于第三方应用 suite token 等接口）。
    /// </summary>
    public async Task<T> PostWithoutAccessTokenAsync<T>(string path, object? body, CancellationToken ct = default)
        where T : WeComApiResult
    {
        var url = BuildUrl(path);

        using var response = body is null
            ? await _http.PostAsync(url, null, ct)
            : await _http.PostAsJsonAsync(url, body, JsonOptions, ct);

        return await ReadApiResultAsync<T>(response, $"POST {path}", url, ct);
    }

    /// <summary>
    /// 发送 Multipart POST 请求（文件上传等场景）
    /// </summary>
    /// <typeparam name="T">响应类型</typeparam>
    /// <param name="path">API 路径</param>
    /// <param name="type">素材类型</param>
    /// <param name="fileName">文件名</param>
    /// <param name="fileStream">文件流（调用方负责释放）</param>
    /// <param name="ct">取消令牌</param>
    public async Task<T> PostMultipartAsync<T>(string path, string type, string fileName, Stream fileStream, CancellationToken ct = default)
        where T : WeComApiResult
    {
        var token = await GetAccessTokenAsync(ct);
        try
        {
            return await PostMultipartWithAccessTokenAsync<T>(path, token, type, fileName, fileStream, ct);
        }
        catch (WeComApiException ex) when (IsAccessTokenError(ex) && fileStream.CanSeek)
        {
            InvalidateToken();
            fileStream.Position = 0;
            token = await GetAccessTokenAsync(ct);
            return await PostMultipartWithAccessTokenAsync<T>(path, token, type, fileName, fileStream, ct);
        }
    }

    private async Task<T> PostMultipartWithAccessTokenAsync<T>(
        string path,
        string accessToken,
        string type,
        string fileName,
        Stream fileStream,
        CancellationToken ct)
        where T : WeComApiResult
    {
        var url = $"{NormalizeBaseUrl(_options.ApiUrl)}{path}?access_token={Uri.EscapeDataString(accessToken)}&type={Uri.EscapeDataString(type)}";

        using var content = new MultipartFormDataContent();
        var streamContent = new NonDisposingStreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(streamContent, "media", fileName);

        using var response = await _http.PostAsync(url, content, ct);
        return await ReadApiResultAsync<T>(response, $"POST {path} 上传", url, ct);
    }

    /// <summary>
    /// 反序列化企业微信 API 响应并检查 errcode。
    /// HTTP 层失败（非 2xx）与业务层失败（errcode != 0）均抛出携带 RequestPath（已脱敏）/ HttpStatus 的
    /// <see cref="WeComApiException"/>，避免泄漏敏感查询参数。
    /// </summary>
    public static Task<T> ReadApiResultAsync<T>(HttpResponseMessage response, string operation, CancellationToken ct = default)
        where T : WeComApiResult
        => ReadApiResultAsync<T>(response, operation, requestUrl: null, ct);

    /// <summary>
    /// 反序列化企业微信 API 响应并检查 errcode（可传入请求 URL 兜底）。
    /// HTTP 层失败（非 2xx）与业务层失败（errcode != 0）均抛出携带 RequestPath（已脱敏）/ HttpStatus 的
    /// <see cref="WeComApiException"/>，避免泄漏敏感查询参数。
    /// </summary>
    /// <param name="requestUrl">
    /// 请求 URL 兜底：当自定义 <see cref="HttpMessageHandler"/> 返回未带 RequestMessage 的响应时，
    /// <c>response.RequestMessage?.RequestUri</c> 可能为 null；此时使用本参数还原 RequestPath。
    /// </param>
    public static async Task<T> ReadApiResultAsync<T>(HttpResponseMessage response, string operation, string? requestUrl, CancellationToken ct = default)
        where T : WeComApiResult
    {
        var path = response.RequestMessage?.RequestUri?.ToString() ?? requestUrl;

        if (!response.IsSuccessStatusCode)
        {
            throw new WeComApiException(
                -1,
                $"{operation} HTTP 失败：{(int)response.StatusCode}",
                path,
                response.StatusCode);
        }

        T? result;
        try
        {
            result = await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new WeComApiException(
                -1,
                $"{operation} 响应反序列化失败：{ex.Message}",
                path,
                response.StatusCode);
        }

        if (result is null)
        {
            throw new WeComApiException(
                -1,
                $"{operation} 响应为空",
                path,
                response.StatusCode);
        }

        if (result.ErrCode != 0)
        {
            throw new WeComApiException(
                result.ErrCode,
                result.ErrMsg,
                path,
                response.StatusCode);
        }

        return result;
    }

    /// <summary>
    /// 清除缓存的 Token（强制下一轮重新获取）
    /// </summary>
    public void InvalidateToken()
    {
        _token = null;
        _tokenExpiry = DateTime.MinValue;
    }

    public void Dispose()
    {
        _tokenLock.Dispose();
    }

    #region 内部方法

    private static string NormalizeBaseUrl(string apiUrl)
    {
        return apiUrl.TrimEnd('/');
    }

    private string BuildUrl(string path, IDictionary<string, string>? extraParams = null, string? accessToken = null)
    {
        var query = new List<string>();

        if (extraParams is not null)
        {
            foreach (var kv in extraParams)
                query.Add($"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}");
        }

        if (accessToken is not null)
            query.Add($"access_token={Uri.EscapeDataString(accessToken)}");

        var baseUrl = NormalizeBaseUrl(_options.ApiUrl);
        var url = $"{baseUrl}{path}";

        if (query.Count > 0)
            url += (path.Contains('?', StringComparison.Ordinal) ? "&" : "?") + string.Join("&", query);

        return url;
    }

    private static bool IsAccessTokenError(WeComApiException ex)
    {
        return AccessTokenErrorCodes.Contains(ex.ErrorCode);
    }

    private sealed class NonDisposingStreamContent : StreamContent
    {
        public NonDisposingStreamContent(Stream content) : base(content)
        {
        }

        protected override void Dispose(bool disposing)
        {
            // MultipartContent owns the StreamContent wrapper, not the caller's stream.
        }
    }

    #endregion
}
