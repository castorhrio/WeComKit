# WeComKit.Api

`WeComKit.Api` 是企业微信常用 API 的轻量客户端，基于 `HttpClient` 实现。自建应用接口通过 `WeComHttpClient` 维护 AccessToken 缓存；机器人和第三方应用 suite 接口保持薄封装，不接管业务持久化。

`WeComKit.Api` is a small managed client for common WeCom APIs. App APIs keep access-token caching inside `WeComHttpClient`; bot and third-party suite APIs stay thin and do not own business persistence.

## 功能 / Features

- AccessToken 获取和缓存。 / Access token acquisition and caching.
- 部门创建、更新、删除、列表查询。 / Department create, update, delete, and list APIs.
- 成员创建、更新、删除、查询、列表。 / User create, update, delete, get, and list APIs.
- 临时素材上传和永久图片上传。 / Temporary media upload and permanent image upload.
- 应用消息推送：文本、图片、语音、视频、文件、文本卡片、图文、Markdown、模板卡片。 / App message sending: text, image, voice, video, file, text card, news, markdown, and template card.
- OAuth 网页授权：授权 URL、`auth/getuserinfo`、`auth/getuserdetail`。 / OAuth: authorization URL, `auth/getuserinfo`, and `auth/getuserdetail`.
- 群机器人 webhook 和回调 `response_url` 回复。 / Bot webhook and callback `response_url` replies.
- 第三方应用 suite：`suite_access_token`、预授权码、永久授权码、授权企业 token。 / Third-party suite: `suite_access_token`, pre-auth code, permanent code, and authorized corp token.

## 安装 / Install

```powershell
dotnet add package WeComKit.Api --version 1.0.0
```

## 依赖注入注册 / Register with Dependency Injection

```csharp
using WeComKit.Api.Extensions;

services.AddWeComKit(options =>
{
    options.CorpId = "your-corp-id";
    options.AgentId = "1000002";
    options.Secret = "your-app-secret";
});

services.AddWeComBotKit(options =>
{
    options.WebhookKey = "your-bot-key";
});

services.AddWeComSuiteKit(options =>
{
    options.SuiteId = "your-suite-id";
    options.SuiteSecret = "your-suite-secret";
});
```

## 发送文本消息 / Send a Text Message

```csharp
using WeComKit.Api;
using WeComKit.Api.Models;

public sealed class NoticeService
{
    private readonly WeComMessageApi _messages;

    public NoticeService(WeComMessageApi messages)
    {
        _messages = messages;
    }

    public Task SendAsync(string userId, string content, CancellationToken ct)
    {
        return _messages.SendTextAsync(new TextMessageRequest
        {
            ToUser = userId,
            Text = new TextMessage { Content = content }
        }, ct);
    }
}
```

## 上传素材 / Upload Media

```csharp
await using var stream = File.OpenRead("notice.png");
var media = await mediaApi.UploadAsync("image", "notice.png", stream, ct);
```

上传 API 不会释放调用方传入的流。

The upload API does not dispose the caller-provided stream.

## OAuth 网页授权 / OAuth

```csharp
var url = oauthApi.BuildAuthorizeUrl("https://example.com/wecom/callback", "state");
var identity = await oauthApi.GetUserInfoAsync(code, ct);

if (!string.IsNullOrEmpty(identity.UserTicket))
{
    var detail = await oauthApi.GetUserDetailAsync(identity.UserTicket, ct);
}
```

## 群机器人 / Bot

```csharp
await botApi.SendWebhookTextAsync("任务完成", ct: ct);
await botApi.SendResponseMarkdownAsync(responseUrl, "**已收到**", ct);
```

## 第三方应用 suite / Third-Party Suite

```csharp
var suiteToken = await suiteApi.GetSuiteTokenAsync(suiteTicket, ct);
var preAuth = await suiteApi.GetPreAuthCodeAsync(suiteToken.SuiteAccessToken, ct);
var permanent = await suiteApi.GetPermanentCodeAsync(suiteToken.SuiteAccessToken, authCode, ct);
var corpToken = await suiteApi.GetCorpTokenAsync(
    suiteToken.SuiteAccessToken,
    permanent.AuthCorpInfo!.CorpId,
    permanent.PermanentCode,
    ct);
```

`suite_ticket`、`permanent_code` 和授权企业 token 的持久化策略差异较大，本包只封装官方 API，不内置数据库存储。

Persistence for `suite_ticket`, `permanent_code`, and authorized corp tokens varies by application. This package wraps the official APIs and does not include database storage.

## 多应用 / 多企业 / Multi-App Usage

内置 DI 扩展注册一个 `WeComOptions` 和一个 `WeComHttpClient`。如果同一宿主进程需要访问多个企业或多个应用，请在宿主项目中分别注册客户端，或显式构造不同配置的 `WeComHttpClient` 和 API 类。

The built-in DI extension registers one `WeComOptions` instance and one `WeComHttpClient`. For multiple enterprises or apps, register separate clients in the host application or construct separate `WeComHttpClient` instances with explicit options.
