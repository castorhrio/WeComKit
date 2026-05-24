using System.Net;
using System.Text;
using WeComKit.Api;
using WeComKit.Api.Http;

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
}
