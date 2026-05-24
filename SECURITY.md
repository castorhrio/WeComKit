# 安全策略

[English](#english)

## 支持版本

当前稳定线为 `1.x`。安全修复会优先发布到最新的 `1.x` 版本。破坏性安全修复会在发布说明中明确标注影响范围和迁移方式。

## 报告安全问题

请不要在公开 issue 中发布以下内容：

- 企业微信 CorpId、Secret、SuiteSecret、Token、EncodingAESKey。
- 私钥、回调密文、会话存档明文或真实消息 payload。
- 可访问生产环境的 URL、日志、配置文件或凭据。

如果你发现安全漏洞，请通过仓库维护者提供的私有联系方式报告。报告中可以包含复现步骤、受影响版本、预期影响和建议修复方式，但请避免附带真实凭据或生产数据。

## 原生 SDK 二进制文件

`WeComKit.MsgAudit` 不重新分发、也不会自动下载腾讯官方会话存档 native SDK。使用方需要从企业微信官方渠道获取 native 文件，并自行校验来源、部署路径和运行环境依赖。

## 密钥处理建议

- 不要把企业微信 Secret、SuiteSecret、EncodingAESKey 或 RSA 私钥提交到仓库。
- 在应用中使用安全的配置源或密钥管理服务。
- 诊断日志中避免输出完整密钥、明文消息和敏感回调参数。
- 生产环境中应限制 native SDK 文件目录的写权限。

---

## English

# Security Policy

## Supported Versions

The current stable line is `1.x`. Security fixes are prioritized for the latest `1.x` release. Breaking security fixes will document impact and migration guidance in release notes.

## Reporting a Vulnerability

Do not publish the following in public issues:

- WeCom CorpId, Secret, SuiteSecret, Token, or EncodingAESKey.
- Private keys, encrypted callback bodies, decrypted archive messages, or real message payloads.
- Production URLs, logs, configuration files, or credentials.

If you find a vulnerability, report it through the repository maintainer's private contact channel. Include reproduction steps, affected versions, expected impact, and suggested fixes when possible, but avoid real credentials or production data.

## Native SDK Binaries

`WeComKit.MsgAudit` does not redistribute or automatically download Tencent's official message-audit native SDK. Consumers are responsible for obtaining native files from official WeCom channels and verifying their source, deployment path, and runtime dependencies.

## Secret Handling

- Do not commit WeCom secrets, SuiteSecret values, EncodingAESKey values, or RSA private keys.
- Use secure configuration sources or secret management services in applications.
- Avoid logging full secrets, plaintext messages, or sensitive callback parameters.
- Restrict write access to native SDK directories in production environments.
