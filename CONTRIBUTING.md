# 贡献指南

[English](#english)

感谢你关注 WeComKit。这个项目希望保持清晰、稳定、可预测，重点封装企业微信集成中的基础协议、常用 API 和会话存档 native SDK。

## 贡献范围

适合提交的内容：

- 修复协议、签名、加解密、HTTP API 或 native SDK 封装中的问题。
- 补充企业微信官方 API 的通用封装。
- 改进文档、示例、诊断信息和测试覆盖。
- 提升跨平台兼容性，尤其是 Windows / Linux 下的 native SDK 加载体验。

不适合放入通用包的内容：

- 特定应用的数据模型、存储实现或产品流程。
- 对特定外部产品或服务的专用集成。
- 需要真实企业微信凭据才能稳定运行的单元测试。
- 运行时自动下载或重新分发腾讯官方 native SDK 二进制文件。

## 开发约定

- 保持 `Core`、`Api`、`MsgAudit` 的职责边界清晰。
- 优先使用显式配置和可预测失败，不隐藏关键部署要求。
- 公共 API 变更需要谨慎，`1.x` 阶段应保持向后兼容。
- 能用单元测试验证的行为应补充测试。
- 文档默认中文优先，同时提供英文说明。

## 本地验证

```powershell
dotnet restore .\WeComKit.sln
dotnet test .\WeComKit.sln -c Release
dotnet pack .\WeComKit.sln -c Release --no-build -o .\artifacts\packages
```

## Pull Request 建议

- 一个 PR 尽量只解决一个明确问题。
- 描述改动原因、行为变化和验证方式。
- 如果修改公开 API，请说明兼容性影响。
- 如果修改 `WeComKit.MsgAudit`，请说明 native SDK 文件发现、加载或部署行为是否变化。

---

## English

Thank you for your interest in WeComKit. The project aims to stay clear, stable, and predictable while wrapping reusable WeCom protocol helpers, common APIs, and the official message-audit native SDK.

## Contribution Scope

Good contributions include:

- Fixes for protocol helpers, signatures, cryptography, HTTP APIs, or native SDK wrappers.
- Common wrappers for official WeCom APIs.
- Documentation, examples, diagnostics, and test coverage improvements.
- Cross-platform improvements, especially native SDK loading on Windows and Linux.

Please keep these outside the shared packages:

- Application-specific data models, storage implementations, or product workflows.
- Integrations dedicated to a specific external product or service.
- Unit tests that require real WeCom credentials to run reliably.
- Runtime downloads or redistribution of Tencent's official native SDK binaries.

## Development Guidelines

- Keep `Core`, `Api`, and `MsgAudit` responsibilities separate.
- Prefer explicit configuration and predictable failures.
- Treat public API changes carefully. The `1.x` line should remain backward compatible.
- Add tests for behavior that can be verified without real credentials.
- Documentation should be Chinese-first with an English section.

## Local Checks

```powershell
dotnet restore .\WeComKit.sln
dotnet test .\WeComKit.sln -c Release
dotnet pack .\WeComKit.sln -c Release --no-build -o .\artifacts\packages
```

## Pull Requests

- Keep each PR focused on one clear problem.
- Describe why the change is needed, what behavior changed, and how it was verified.
- If public APIs change, explain compatibility impact.
- If `WeComKit.MsgAudit` changes, explain any native SDK discovery, loading, or deployment impact.
