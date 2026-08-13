using System.Net;
using System.Reflection;
using System.Text;
using WeComKit.Api;
using WeComKit.Api.Http;
using WeComKit.Api.Models;

namespace WeComKit.Tests;

public class WeComHttpClientTests
{
    [Fact]
    public async Task GetAccessTokenAsync_CachesTokenUntilExpiry()
    {
        var handler = new QueueHandler(
            JsonResponse("""{"errcode":0,"errmsg":"ok","access_token":"token-1","expires_in":7200}"""));
        var client = CreateClient(handler);

        var first = await client.GetAccessTokenAsync();
        var second = await client.GetAccessTokenAsync();

        Assert.Equal("token-1", first);
        Assert.Equal("token-1", second);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task PostMultipartAsync_DoesNotDisposeCallerStream()
    {
        var handler = new QueueHandler(
            JsonResponse("""{"errcode":0,"errmsg":"ok","access_token":"token-1","expires_in":7200}"""),
            JsonResponse("""{"errcode":0,"errmsg":"ok"}"""));
        var client = CreateClient(handler);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("content"));

        await client.PostMultipartAsync<TestApiResult>("/cgi-bin/media/upload", "file", "a.txt", stream);

        Assert.True(stream.CanRead);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task GetAsync_RefreshesTokenOnceWhenAccessTokenExpires()
    {
        var handler = new QueueHandler(
            JsonResponse("""{"errcode":0,"errmsg":"ok","access_token":"token-1","expires_in":7200}"""),
            JsonResponse("""{"errcode":42001,"errmsg":"access_token expired"}"""),
            JsonResponse("""{"errcode":0,"errmsg":"ok","access_token":"token-2","expires_in":7200}"""),
            JsonResponse("""{"errcode":0,"errmsg":"ok"}"""));
        var client = CreateClient(handler);

        await client.GetAsync<TestApiResult>("/cgi-bin/user/get", new Dictionary<string, string>
        {
            ["userid"] = "user1"
        });

        Assert.Equal(4, handler.Requests.Count);
        Assert.Contains("access_token=token-1", handler.Requests[1].RequestUri!.Query);
        Assert.Contains("access_token=token-2", handler.Requests[3].RequestUri!.Query);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ConcurrentCallersTriggerExactlyOneRefresh()
    {
        // single-flight：100 个并发调用只应触发 1 次 token 请求
        var handler = new CountingHandler(
            """{"errcode":0,"errmsg":"ok","access_token":"token-concurrent","expires_in":7200}""");
        var client = CreateClient(handler);

        var tasks = Enumerable.Range(0, 100)
            .Select(_ => client.GetAccessTokenAsync())
            .ToArray();
        var tokens = await Task.WhenAll(tasks);

        Assert.All(tokens, t => Assert.Equal("token-concurrent", t));
        Assert.Equal(1, handler.TokenRequestCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_HonorsConfigurableRefreshSkew()
    {
        // skew=1 分钟，expires_in=120s → 缓存有效期 ≈ 60s
        // 立即第二次调用应命中缓存（仅 1 次请求）
        var handler = new QueueHandler(
            JsonResponse("""{"errcode":0,"errmsg":"ok","access_token":"token-skew","expires_in":120}"""));
        var options = BaseOptions();
        options.TokenRefreshSkew = TimeSpan.FromMinutes(1);
        var client = CreateClientWithOptions(handler, options);

        var first = await client.GetAccessTokenAsync();
        var second = await client.GetAccessTokenAsync();

        Assert.Equal("token-skew", first);
        Assert.Equal("token-skew", second);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ClampsSkewWhenLargerThanLifetime_StillCaches()
    {
        // expires_in=60s, skew=5min → clamp 到 30s，token 仍被缓存（避免排队打爆）
        var handler = new QueueHandler(
            JsonResponse("""{"errcode":0,"errmsg":"ok","access_token":"token-clamp","expires_in":60}"""));
        var options = BaseOptions();
        options.TokenRefreshSkew = TimeSpan.FromMinutes(5);
        var client = CreateClientWithOptions(handler, options);

        var first = await client.GetAccessTokenAsync();
        var second = await client.GetAccessTokenAsync();

        Assert.Equal("token-clamp", first);
        Assert.Equal("token-clamp", second);
        Assert.Single(handler.Requests); // 并发/连续调用都命中同一缓存
    }

    [Fact]
    public async Task GetAccessTokenAsync_NegativeSkewViaConfigBinder_IsClampedToZero()
    {
        // 模拟 IConfiguration 配置绑定器：它直接写 backing field，绕过 setter 的非负校验。
        // 取用点必须再做一次非负钳制，否则负 skew 会把缓存有效期延长到真实过期之后。
        //
        // 区分“钳制”与“缓存延长”：用很短的 expires_in，等待其过期后再调用。
        // 若 skew 被正确钳为 0，过期后应重新获取（第 2 次请求）；
        // 若未钳制（bug），负 skew 会把有效期延长 5 分钟，第 2 次仍命中旧缓存 → 测试失败。
        var handler = new QueueHandler(
            JsonResponse("""{"errcode":0,"errmsg":"ok","access_token":"token-a","expires_in":2}"""),
            JsonResponse("""{"errcode":0,"errmsg":"ok","access_token":"token-b","expires_in":2}"""));
        var options = BaseOptions();
        // 反射写入 backing field，等效于 binder 行为（绕过 setter 的非负校验）
        typeof(WeComOptions)
            .GetField("_tokenRefreshSkew", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(options, TimeSpan.FromMinutes(-5));
        var client = CreateClientWithOptions(handler, options);

        var first = await client.GetAccessTokenAsync();
        Assert.Equal("token-a", first);
        Assert.Single(handler.Requests);

        // 等待超过 expires_in（2s）后，钳制为 0 的 skew 会让缓存失效 → 第 2 次应刷新
        await Task.Delay(TimeSpan.FromSeconds(3));

        var second = await client.GetAccessTokenAsync();
        Assert.Equal("token-b", second);     // 刷新成功
        Assert.Equal(2, handler.Requests.Count); // 证明不是命中旧缓存（否则只有 1 次请求）
    }

    [Fact]
    public async Task GetAccessTokenAsync_DoesNotCacheWhenExpiresInInvalid()
    {
        // expires_in=0 → 非法，不缓存；下一次调用重新获取
        var handler = new QueueHandler(
            JsonResponse("""{"errcode":0,"errmsg":"ok","access_token":"token-a","expires_in":0}"""),
            JsonResponse("""{"errcode":0,"errmsg":"ok","access_token":"token-b","expires_in":7200}"""));
        var client = CreateClient(handler);

        var first = await client.GetAccessTokenAsync();
        var second = await client.GetAccessTokenAsync();

        Assert.Equal("token-a", first);
        Assert.Equal("token-b", second);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task GetAccessTokenAsync_PropagatesRefreshFailure()
    {
        // 服务端返回 errcode != 0 → 抛 WeComApiException
        var handler = new QueueHandler(
            JsonResponse("""{"errcode":40029,"errmsg":"invalid code"}"""));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<WeComApiException>(() => client.GetAccessTokenAsync());
    }

    [Fact]
    public async Task GetAccessTokenAsync_RespectsCancellation()
    {
        var handler = new NeverRespondingHandler();
        var client = CreateClient(handler);
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAccessTokenAsync(cts.Token));
    }

    [Fact]
    public void TokenRefreshSkew_RejectsNegativeValue()
    {
        var options = BaseOptions();
        Assert.Throws<ArgumentOutOfRangeException>(() => options.TokenRefreshSkew = TimeSpan.FromMinutes(-1));
    }

    [Fact]
    public async Task GetAsync_HttpFailure_ThrowsEnrichedExceptionWithPathAndStatus()
    {
        // token 成功，但业务请求返回 500 → 应抛出携带 RequestPath（脱敏）/ HttpStatus 的异常
        var handler = new QueueHandler(
            JsonResponse("""{"errcode":0,"errmsg":"ok","access_token":"tok","expires_in":7200}"""),
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                RequestMessage = new HttpRequestMessage(HttpMethod.Get,
                    "https://example.test/cgi-bin/user/get?access_token=SECRET&userid=zhangsan")
            });
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<WeComApiException>(() =>
            client.GetAsync<TestApiResult>("/cgi-bin/user/get", new Dictionary<string, string> { ["userid"] = "zhangsan" }));

        Assert.Equal(HttpStatusCode.InternalServerError, ex.HttpStatus);
        Assert.NotNull(ex.RequestPath);
        Assert.DoesNotContain("SECRET", ex.RequestPath!);     // access_token 已脱敏
        Assert.Contains("access_token=***REDACTED***", ex.RequestPath!);
        Assert.Contains("userid=zhangsan", ex.RequestPath!);  // 非敏感参数保留
    }

    [Fact]
    public async Task GetAsync_BusinessError_PreservesErrorCodeAndEnrichesPath()
    {
        // HTTP 200 但 errcode != 0 → 异常携带业务错误码 + path/status
        var handler = new QueueHandler(
            JsonResponse("""{"errcode":0,"errmsg":"ok","access_token":"tok","expires_in":7200}"""),
            JsonResponse("""{"errcode":60011,"errmsg":"no privilege to access"}"""));
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<WeComApiException>(() =>
            client.GetAsync<TestApiResult>("/cgi-bin/user/get"));

        Assert.Equal(60011, ex.ErrorCode);
        Assert.Equal("no privilege to access", ex.ErrorMessage);
        Assert.Equal(HttpStatusCode.OK, ex.HttpStatus);
        Assert.NotNull(ex.RequestPath);
    }

    [Fact]
    public async Task GetAccessTokenAsync_HttpFailure_ThrowsEnrichedException()
    {
        // gettoken 返回 503 → 携带 status/path 的异常
        var handler = new QueueHandler(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                RequestMessage = new HttpRequestMessage(HttpMethod.Get,
                    "https://example.test/cgi-bin/gettoken?corpid=corp&corpsecret=SHHHH")
            });
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<WeComApiException>(() => client.GetAccessTokenAsync());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.HttpStatus);
        Assert.DoesNotContain("SHHHH", ex.RequestPath!);
        Assert.Contains("corpsecret=***REDACTED***", ex.RequestPath!);
    }

    [Fact]
    public async Task GetAsync_ResponseWithoutRequestMessage_FallsBackToConstructedUrl()
    {
        // 自定义 handler 返回不带 RequestMessage 的响应（如某些 mock handler）。
        // RequestPath 必须从已构造的请求 URL 还原，并脱敏 access_token。
        var handler = new QueueHandler(
            JsonResponse("""{"errcode":0,"errmsg":"ok","access_token":"tok","expires_in":7200}"""),
            new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("upstream down", Encoding.UTF8, "text/plain") });
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<WeComApiException>(() =>
            client.GetAsync<TestApiResult>("/cgi-bin/user/get", new Dictionary<string, string> { ["userid"] = "zhangsan" }));

        Assert.Equal(HttpStatusCode.BadGateway, ex.HttpStatus);
        Assert.NotNull(ex.RequestPath);
        Assert.NotEmpty(ex.RequestPath!);
        Assert.Contains("/cgi-bin/user/get", ex.RequestPath!);
        Assert.Contains("access_token=***REDACTED***", ex.RequestPath!); // 兜底 URL 仍经脱敏
        Assert.Contains("userid=zhangsan", ex.RequestPath!);
    }

    [Fact]
    public async Task GetAccessTokenAsync_BusinessError_EnrichesPath()
    {
        // QueueHandler 返回的响应需显式带上 RequestMessage，才能还原 RequestPath
        var response = JsonResponse("""{"errcode":40029,"errmsg":"invalid code"}""");
        response.RequestMessage = new HttpRequestMessage(HttpMethod.Get,
            "https://example.test/cgi-bin/gettoken?corpid=corp&corpsecret=SHHHH");
        var handler = new QueueHandler(response);
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<WeComApiException>(() => client.GetAccessTokenAsync());

        Assert.Equal(40029, ex.ErrorCode);
        Assert.Equal("invalid code", ex.ErrorMessage);
        Assert.Equal(HttpStatusCode.OK, ex.HttpStatus);
        Assert.Contains("gettoken", ex.RequestPath!);
        Assert.DoesNotContain("SHHHH", ex.RequestPath!); // corpsecret 脱敏
    }

    private static WeComHttpClient CreateClient(HttpMessageHandler handler)
    {
        return new WeComHttpClient(new HttpClient(handler), new WeComOptions
        {
            ApiUrl = "https://example.test",
            CorpId = "corp-id",
            Secret = "secret",
            AgentId = "1000002"
        });
    }

    private static WeComHttpClient CreateClientWithOptions(HttpMessageHandler handler, WeComOptions options)
    {
        return new WeComHttpClient(new HttpClient(handler), options);
    }

    private static WeComOptions BaseOptions() => new()
    {
        ApiUrl = "https://example.test",
        CorpId = "corp-id",
        Secret = "secret",
        AgentId = "1000002"
    };

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class QueueHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public QueueHandler(params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (_responses.Count == 0)
                throw new InvalidOperationException("No queued HTTP response.");

            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class TestApiResult : WeComKit.Api.Models.WeComApiResult
    {
    }

    /// <summary>
    /// 对所有请求返回同一响应，并统计 gettoken 请求次数（用于 single-flight 验证）。
    /// </summary>
    private sealed class CountingHandler : HttpMessageHandler
    {
        private readonly string _tokenJson;
        public int TokenRequestCount;

        public CountingHandler(string tokenJson) => _tokenJson = tokenJson;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/gettoken", StringComparison.Ordinal))
                Interlocked.Increment(ref TokenRequestCount);

            // 模拟少量网络延迟，放大并发竞争窗口
            return Task.Delay(5, cancellationToken).ContinueWith(
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_tokenJson, Encoding.UTF8, "application/json")
                },
                cancellationToken,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }
    }

    /// <summary>永不响应的处理程序（用于取消测试）。阻塞直到取消令牌触发。</summary>
    private sealed class NeverRespondingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // 用一个永不完成的 TCS，由传入的 cancellationToken 注册取消回调来触发 TaskCanceledException
            var tcs = new TaskCompletionSource<HttpResponseMessage>();
            cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
            return tcs.Task;
        }
    }
}
