# 更新日志

[English](#english)

本项目遵循语义化版本控制。`1.x` 阶段保持公开 API 向后兼容；破坏性变更会进入新的主版本。

## 1.0.0

- 发布第一版稳定包集合。
- 增加 OAuth、群机器人 webhook / response_url、第三方应用 suite 授权客户端。
- 增加常见 AccessToken 失效错误的一次自动刷新重试。
- 会话存档 SDK 文件复制改为显式启用，并移除 transitive build target 注入。
- 增加无需企业微信凭据的 native SDK 加载诊断。
- 将 API 模型拆分为 token、department、user、media、message、bot、OAuth、suite 等文件。
- 更新中英文 README 和 NuGet 元数据。

## 0.1.0

- 初始拆分为 `WeComKit.Core`、`WeComKit.Api`、`WeComKit.MsgAudit` 三个包。
- 增加企业微信 / 微信公众号消息加解密和签名工具。
- 增加企业微信 AccessToken、通讯录、素材上传和应用消息 API 客户端。
- 增加企业微信会话存档官方 native SDK 的托管封装。
- 增加基础测试、NuGet 包元数据、README 和许可证文件。

---

## English

# Changelog

This project follows semantic versioning. The `1.x` line keeps public APIs backward compatible; breaking changes require a new major version.

## 1.0.0

- Released the first stable package set.
- Added OAuth, bot webhook / response_url, and third-party suite authorization clients.
- Added one-time automatic refresh retry for common AccessToken expiration errors.
- Made message-audit SDK file copying opt-in and removed transitive build target injection.
- Added native SDK load diagnostics without requiring WeCom credentials.
- Split API models into token, department, user, media, message, bot, OAuth, and suite files.
- Updated bilingual README files and NuGet metadata.

## 0.1.0

- Split the project into `WeComKit.Core`, `WeComKit.Api`, and `WeComKit.MsgAudit`.
- Added WeCom / WeChat message encryption, decryption, and signature utilities.
- Added WeCom clients for AccessToken, contacts, media upload, and app messages.
- Added a managed wrapper for the official WeCom message-audit native SDK.
- Added basic tests, NuGet package metadata, README, and license files.
