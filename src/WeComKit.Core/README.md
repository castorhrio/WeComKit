# WeComKit.Core

`WeComKit.Core` 提供企业微信 / 微信公众号集成中最基础的协议工具，不依赖 HTTP 客户端，可单独用于回调处理、后台服务或 Web API。

`WeComKit.Core` provides protocol-level helpers for WeCom / WeChat Work and WeChat public account integrations. It has no HTTP client dependency and can be used independently in callback handlers, background workers, and web APIs.

## 功能 / Features

- 回调签名验证。 / Callback signature verification.
- 企业微信 / 微信公众号消息加解密。 / WeCom / WeChat callback message encryption and decryption.
- 会话存档 `encrypt_random_key` RSA 解密辅助。 / RSA helper for message-audit `encrypt_random_key`.
- 微信小程序加密数据解密。 / WeChat mini-program encrypted data decryption.

## 安装 / Install

```powershell
dotnet add package WeComKit.Core --version 1.0.0
```

## 回调消息解密 / Callback Decryption

```csharp
using WeComKit.Core;

var crypt = new WeComMessageCrypt(token, encodingAesKey, corpId);
var plainXml = crypt.VerifyAndDecrypt(msgSignature, timestamp, nonce, encrypted);
```

## 签名验证 / Signature Verification

```csharp
using WeComKit.Core;

var ok = WeComUtility.VerifySignature(token, signature, timestamp, nonce);
```

## 注意事项 / Notes

- `EncodingAESKey` 必须是企业微信或微信后台配置的 43 位密钥。 / `EncodingAESKey` must be the 43-character value configured in WeCom or WeChat.
- 解密时如果提供 CorpId/AppId，会校验消息尾部的 CorpId/AppId。 / CorpId/AppId is checked during message decryption when provided.
- 企业微信机器人回调的 receiveid 可能为空，此时不要传入 CorpId 做尾部校验。 / Bot callbacks may have an empty receiveid; in that case do not pass CorpId for tail validation.
- 无效 padding、无效消息长度、CorpId/AppId 不匹配都会抛异常。 / Invalid padding, invalid message length, and mismatched CorpId/AppId throw.
