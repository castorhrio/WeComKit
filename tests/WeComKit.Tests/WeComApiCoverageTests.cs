using System.Net;
using System.Text;
using WeComKit.Api;
using WeComKit.Api.Http;
using WeComKit.Api.Models;

namespace WeComKit.Tests;

public class WeComApiCoverageTests
{
    [Fact]
    public void BuildAuthorizeUrl_IncludesCorpAgentAndWechatRedirect()
    {
        var api = new WeComOAuthApi(CreateClient(new QueueHandler()), new WeComOptions
        {
            ApiUrl = "https://example.test",
            CorpId = "corp-id",
            AgentId = "1000002",
            Secret = "secret"
        });

        var url = api.BuildAuthorizeUrl("https://app.test/callback?a=1", "state-1");

        Assert.StartsWith("https://open.weixin.qq.com/connect/oauth2/authorize?", url);
        Assert.Contains("appid=corp-id", url);
        Assert.Contains("agentid=1000002", url);
        Assert.Contains("redirect_uri=https%3A%2F%2Fapp.test%2Fcallback%3Fa%3D1", url);
        Assert.EndsWith("#wechat_redirect", url);
    }

    [Fact]
    public async Task SendResponseTextAsync_PostsBotPayload()
    {
        var handler = new QueueHandler(JsonResponse("""{"errcode":0,"errmsg":"ok"}"""));
        var api = new WeComBotApi(new HttpClient(handler), new WeComBotOptions());

        await api.SendResponseTextAsync("https://bot.test/response", "hello", new[] { "user1" });

        Assert.Single(handler.Requests);
        Assert.Equal("https://bot.test/response", handler.Requests[0].RequestUri!.ToString());
        var body = await handler.Requests[0].Content!.ReadAsStringAsync();
        Assert.Contains("\"msgtype\":\"text\"", body);
        Assert.Contains("\"content\":\"hello\"", body);
        Assert.Contains("\"mentioned_list\":[\"user1\"]", body);
    }

    [Fact]
    public async Task GetSuiteTokenAsync_PostsSuiteCredentials()
    {
        var handler = new QueueHandler(JsonResponse("""{"errcode":0,"errmsg":"ok","suite_access_token":"suite-token","expires_in":7200}"""));
        var api = new WeComSuiteApi(new HttpClient(handler), new WeComSuiteOptions
        {
            ApiUrl = "https://example.test",
            SuiteId = "suite-id",
            SuiteSecret = "suite-secret"
        });

        var result = await api.GetSuiteTokenAsync("suite-ticket");

        Assert.Equal("suite-token", result.SuiteAccessToken);
        Assert.Equal("https://example.test/cgi-bin/service/get_suite_token", handler.Requests[0].RequestUri!.ToString());
        var body = await handler.Requests[0].Content!.ReadAsStringAsync();
        Assert.Contains("\"suite_id\":\"suite-id\"", body);
        Assert.Contains("\"suite_secret\":\"suite-secret\"", body);
        Assert.Contains("\"suite_ticket\":\"suite-ticket\"", body);
    }

    [Fact]
    public async Task SendTemplateCardAsync_FillsDefaultAgentId()
    {
        var handler = new QueueHandler(
            JsonResponse("""{"errcode":0,"errmsg":"ok","access_token":"token-1","expires_in":7200}"""),
            JsonResponse("""{"errcode":0,"errmsg":"ok","msgid":"msg-1"}"""));
        var client = CreateClient(handler);
        var api = new WeComMessageApi(client, new WeComOptions
        {
            ApiUrl = "https://example.test",
            CorpId = "corp-id",
            Secret = "secret",
            AgentId = "1000002"
        });

        await api.SendTemplateCardAsync(new TemplateCardMessageRequest
        {
            ToUser = "user1",
            TemplateCard = new { card_type = "text_notice" }
        });

        var body = await handler.Requests[1].Content!.ReadAsStringAsync();
        Assert.Contains("\"msgtype\":\"template_card\"", body);
        Assert.Contains("\"agentid\":1000002", body);
        Assert.Contains("\"template_card\":{\"card_type\":\"text_notice\"}", body);
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
}
