using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using WeComKit.Api.Models;

namespace WeComKit.Api.Http;

/// <summary>
/// 企业微信 API HTTP 客户端，内置 AccessToken 自动获取、缓存和刷新
/// </summary>
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
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<WeComTokenResponse>(cancellationToken: ct);

            if (result is null)
                throw new WeComApiException(-1, "获取 AccessToken 响应反序列化失败");

            result.EnsureSuccess();

            _token = result.AccessToken;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(result.ExpiresIn - 300); // 提前 5 分钟刷新
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
        return await ReadApiResultAsync<T>(response, $"GET {path}", ct);
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

        return await ReadApiResultAsync<T>(response, $"POST {path}", ct);
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

        return await ReadApiResultAsync<T>(response, $"POST {path}", ct);
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
        return await ReadApiResultAsync<T>(response, $"POST {path} 上传", ct);
    }

    /// <summary>
    /// 反序列化企业微信 API 响应并检查 errcode。
    /// </summary>
    public static async Task<T> ReadApiResultAsync<T>(HttpResponseMessage response, string operation, CancellationToken ct = default)
        where T : WeComApiResult
    {
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);

        if (result is null)
            throw new WeComApiException(-1, $"{operation} 响应反序列化失败");

        result.EnsureSuccess();
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
