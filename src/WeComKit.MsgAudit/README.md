# WeComKit.MsgAudit

`WeComKit.MsgAudit` 是企业微信会话存档官方原生 SDK 的托管封装。它会串行化访问原生 `Sdk*` 指针，提供适合 .NET 应用使用的同步/异步方法，并保持原生 SDK 部署显式可控。

`WeComKit.MsgAudit` is a managed wrapper for the official WeCom message-audit native SDK. It serializes calls to the native `Sdk*` pointer, exposes sync/async helpers for .NET applications, and keeps native SDK deployment explicit.

## 安装 / Install

```powershell
dotnet add package WeComKit.MsgAudit --version 1.0.0
```

## 原生 SDK 要求 / Native SDK Requirement

本包不包含、也不会自动下载企业微信官方会话存档原生 SDK。请从企业微信管理后台下载会话存档 SDK，并自行部署原生文件。

This package does not include or download the official native message-audit SDK. Download it from the WeCom admin console and deploy the native files yourself.

Windows 通常需要：

Windows usually requires:

- `WeWorkFinanceSdk.dll`
- `libcrypto-3-x64.dll`
- `libssl-3-x64.dll`
- `libcurl-x64.dll`

Linux 需要：

Linux requires:

- `libWeWorkFinanceSdk.so`

Linux 环境还需要确保官方 `.so` 依赖的 OpenSSL / curl 等系统库能被动态链接器找到。不同发行版和官方 SDK 版本可能依赖不同库版本，建议在部署环境中用 `ldd libWeWorkFinanceSdk.so` 先检查。

On Linux, make sure OpenSSL / curl and other dependencies required by the official `.so` are discoverable by the dynamic linker. The exact versions may vary by distribution and SDK release. Run `ldd libWeWorkFinanceSdk.so` in the deployment environment first.

## 配置原生 SDK 路径 / Configure Native SDK Path

任选一种方式：

Use one of these options:

- 设置环境变量 `WECOM_MSG_AUDIT_SDK_DIR` / set environment variable `WECOM_MSG_AUDIT_SDK_DIR`
- 调用 `WeComFinanceSdk.SetSdkDirectory(path)` / call `WeComFinanceSdk.SetSdkDirectory(path)`
- 将原生文件复制到应用输出目录 / copy native files into the application output directory
- 在消费项目中设置 MSBuild 属性 `WeComMsgAuditSdkPath` / set MSBuild property `WeComMsgAuditSdkPath`

```xml
<PropertyGroup>
  <WeComMsgAuditSdkPath>C:\wecom-msg-audit-sdk</WeComMsgAuditSdkPath>
  <WeComMsgAuditCopySdkFiles>true</WeComMsgAuditCopySdkFiles>
</PropertyGroup>
```

MSBuild 自动复制默认关闭，避免 NuGet 包对消费项目产生隐式构建副作用。只有设置 `WeComMsgAuditCopySdkFiles=true` 时才会复制 SDK 目录下的 `.dll` / `.so` 文件到输出目录。

MSBuild copying is opt-in to avoid implicit build side effects in consuming projects. Set `WeComMsgAuditCopySdkFiles=true` to copy `.dll` / `.so` files from the configured SDK directory into the output directory.

## 使用 / Usage

```csharp
using WeComKit.Core;
using WeComKit.MsgAudit;

WeComFinanceSdk.SetSdkDirectory(@"C:\wecom-msg-audit-sdk");

var nativeLoad = WeComFinanceSdk.DiagnoseNativeLoad();

await using var sdk = await WeComFinanceSdk.CreateAndInitAsync(corpId, secret);
var response = await sdk.GetChatDataResponseAsync(seq: 0, limit: 100);

foreach (var item in response.ChatData)
{
    var key = WeComUtility.DecryptRsa(item.EncryptRandomKey, privateKeyPem);
    var record = await sdk.DecryptChatRecordAsync(key, item.EncryptChatMsg);
}
```

## 诊断 / Diagnostics

```csharp
var availability = WeComFinanceSdk.CheckAvailability();
var nativeLoad = WeComFinanceSdk.DiagnoseNativeLoad();
var steps = WeComFinanceSdk.Diagnose(corpId, secret);
```

`DiagnoseNativeLoad` 只检查文件发现和动态库加载，不需要企业微信凭据。`Diagnose` 可能调用原生 SDK 和企业微信服务，适合安装配置检查，不适合作为高频健康检查。

`DiagnoseNativeLoad` checks file discovery and native loading only, without WeCom credentials. `Diagnose` may call the native SDK and WeCom service. Use it for setup checks, not as a hot-path health probe.
