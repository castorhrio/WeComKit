# WeComKit

WeComKit 是一个面向 .NET 8 的企业微信（WeCom / WeChat Work）集成工具库，目标是把企业微信常用但容易写错的协议、HTTP API 和会话存档 SDK 封装成可独立引用的 NuGet 包。

WeComKit is a .NET 8 toolkit for WeCom / WeChat Work integrations. It packages protocol helpers, common HTTP APIs, and the message-audit native SDK wrapper as separate NuGet packages.

当前稳定版是 `1.0.0`，覆盖企业微信基础协议、常用 HTTP API 和会话存档官方 SDK 托管封装。

The current stable release is `1.0.0`, covering protocol helpers, common WeCom HTTP APIs, and the managed wrapper for the official message-audit SDK.

## 包结构 / Packages

| 包 / Package | 用途 / Purpose |
| --- | --- |
| `WeComKit.Core` | 回调签名验证、消息加解密、RSA 解密、小程序加密数据解密。 / Signature verification, message encryption/decryption, RSA helper, and mini-program encrypted data decryption. |
| `WeComKit.Api` | 企业微信通用 API 客户端：AccessToken、OAuth、通讯录、素材、应用消息、群机器人、第三方应用 suite 授权。 / Managed client for common WeCom APIs: access token, OAuth, contacts, media, app messages, bot webhook, and third-party suite authorization. |
| `WeComKit.MsgAudit` | 企业微信会话存档官方原生 SDK 的托管封装。 / Managed wrapper around the official WeCom message-audit native SDK. |

## 设计目标 / Design Goals

- 协议工具、HTTP API、原生 SDK 封装分包，应用只引用自己需要的能力。 / Keep protocol helpers, HTTP clients, and native SDK wrappers in separate packages.
- 不把业务项目的规则放进通用库。 / Keep application-specific business rules out of the library.
- 不在运行时偷偷下载原生 SDK。 / Do not hide native SDK acquisition behind runtime downloads.
- 优先使用显式配置和可预测失败。 / Prefer explicit configuration and predictable failures.
- 第一版保持小而稳定，避免过早承诺过大的 API 面。 / Keep the first release small enough to stabilize before `1.0.0`.

## 非目标 / Non-Goals

这些能力不属于通用 NuGet 包范围：

These are intentionally kept outside the NuGet packages:

- 订单导入、客户匹配、渠道匹配、Excel 模板识别。 / Order import, customer/channel matching, and Excel template matching.
- OSS、数据库实体、业务状态流转、定时任务。 / OSS integration, database entities, business status flows, and scheduled jobs.
- 吉客云或任何特定 ERP / OMS / WMS 对接。 / Jikeyun or any specific ERP / OMS / WMS integration.
- 会话存档消息的业务消费流程。库只负责拉取、解密、下载文件；如何入库和处理由应用决定。 / Business processing of archived messages. The library fetches, decrypts, and downloads; storage and workflow remain application-owned.

## 安装 / Installation

按需安装即可：

Install only the package you need:

```powershell
dotnet add package WeComKit.Core --version 1.0.0
dotnet add package WeComKit.Api --version 1.0.0
dotnet add package WeComKit.MsgAudit --version 1.0.0
```

`WeComKit.Api` 和 `WeComKit.MsgAudit` 会自动引用 `WeComKit.Core`。

`WeComKit.Api` and `WeComKit.MsgAudit` reference `WeComKit.Core` automatically.

## 快速开始 / Quick Start

### 回调消息加解密 / Callback Message Encryption

```csharp
using WeComKit.Core;

var crypt = new WeComMessageCrypt(token, encodingAesKey, corpId);

if (!crypt.VerifySignature(msgSignature, timestamp, nonce, encrypted))
{
    throw new UnauthorizedAccessException("Invalid callback signature.");
}

var plainXml = crypt.DecryptMsg(encrypted);
```

### 企业微信应用 API / WeCom Application API

```csharp
using WeComKit.Api.Extensions;

services.AddWeComKit(options =>
{
    options.CorpId = "your-corp-id";
    options.AgentId = "1000002";
    options.Secret = "your-app-secret";
});
```

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

### OAuth 网页授权 / OAuth

```csharp
var authUrl = oauthApi.BuildAuthorizeUrl("https://example.com/wecom/callback", "state");
var identity = await oauthApi.GetUserInfoAsync(code, ct);

if (!string.IsNullOrEmpty(identity.UserTicket))
{
    var detail = await oauthApi.GetUserDetailAsync(identity.UserTicket, ct);
}
```

### 群机器人 / Bot Webhook

```csharp
await botApi.SendWebhookMarkdownAsync("**Build completed**", ct);
await botApi.SendResponseTextAsync(responseUrl, "收到消息", ct: ct);
```

### 第三方应用 suite / Third-Party Suite

```csharp
var suiteToken = await suiteApi.GetSuiteTokenAsync(suiteTicket, ct);
var preAuth = await suiteApi.GetPreAuthCodeAsync(suiteToken.SuiteAccessToken, ct);
var permanent = await suiteApi.GetPermanentCodeAsync(suiteToken.SuiteAccessToken, authCode, ct);
```

### 会话存档 SDK / Message Audit SDK

`WeComKit.MsgAudit` 不包含、也不会自动下载企业微信官方会话存档原生 SDK。请先从企业微信管理后台下载 SDK，再通过下面任一方式配置路径：

`WeComKit.MsgAudit` does not ship or download the official native message-audit SDK. Download it from the WeCom admin console and configure the path using one of these options:

- 环境变量 `WECOM_MSG_AUDIT_SDK_DIR` / environment variable `WECOM_MSG_AUDIT_SDK_DIR`
- `WeComFinanceSdk.SetSdkDirectory(path)`
- 应用输出目录 / application output directory
- MSBuild 属性 `WeComMsgAuditSdkPath` + `WeComMsgAuditCopySdkFiles=true` / MSBuild properties `WeComMsgAuditSdkPath` + `WeComMsgAuditCopySdkFiles=true`

```csharp
using WeComKit.MsgAudit;

WeComFinanceSdk.SetSdkDirectory(@"C:\wecom-msg-audit-sdk");

var nativeLoad = WeComFinanceSdk.DiagnoseNativeLoad();

await using var sdk = await WeComFinanceSdk.CreateAndInitAsync(corpId, secret);
var response = await sdk.GetChatDataResponseAsync(seq: 0, limit: 100);
```

## 仓库结构 / Repository Layout

```text
src/
  WeComKit.Core/
  WeComKit.Api/
  WeComKit.MsgAudit/
tests/
  WeComKit.Tests/
```

## 构建、测试、打包 / Build, Test, and Pack

```powershell
dotnet restore .\WeComKit.sln
dotnet test .\WeComKit.sln -c Release
dotnet pack .\WeComKit.sln -c Release --no-build -o .\artifacts\packages
```

## 原生 SDK 说明 / Native SDK Notice

企业微信会话存档原生 SDK 由腾讯通过企业微信管理后台分发。本仓库不重新分发这些原生二进制文件。使用方需要自行从官方渠道下载，并按腾讯相关条款部署。

The WeCom message-audit native SDK is distributed by Tencent through the WeCom admin console. This repository does not redistribute those native binaries. Consumers are responsible for obtaining and deploying them from official channels.

Windows 部署时通常需要把 `WeWorkFinanceSdk.dll`、`libcrypto-3-x64.dll`、`libssl-3-x64.dll`、`libcurl-x64.dll` 放在同一目录。Linux 部署时至少需要 `libWeWorkFinanceSdk.so`，并需要确保系统上的 OpenSSL / curl 等依赖能被动态链接器找到。

On Windows, keep `WeWorkFinanceSdk.dll`, `libcrypto-3-x64.dll`, `libssl-3-x64.dll`, and `libcurl-x64.dll` in the same directory. On Linux, deploy at least `libWeWorkFinanceSdk.so` and make sure OpenSSL / curl dependencies are discoverable by the dynamic linker.

## 版本策略 / Versioning

`1.x` 阶段遵循语义化版本控制。补充 API、DTO 可选字段、诊断增强通常进入次版本；破坏性公开 API 变更进入下一个主版本。

The `1.x` line follows semantic versioning. Additive APIs, optional DTO fields, and diagnostics improvements go into minor versions; breaking public API changes require the next major version.
