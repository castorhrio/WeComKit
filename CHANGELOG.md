# Changelog

## 1.0.0

- Marked the package set as the first stable release.
- Added OAuth, bot webhook/response_url, and third-party suite authorization clients.
- Added one-time access-token refresh retry for common token-expired API errors.
- Made message-audit SDK file copying opt-in and removed transitive build target injection.
- Added native SDK load diagnostics without requiring WeCom credentials.
- Split API models into token, department, user, media, message, bot, OAuth, and suite model files.
- Updated bilingual README files and NuGet metadata.

## 0.1.0

- Split the project into `WeComKit.Core`, `WeComKit.Api`, and `WeComKit.MsgAudit`.
- Added enterprise WeChat message encryption/decryption and signature utilities.
- Added enterprise WeChat API clients for token, contacts, media upload, and app messages.
- Added managed wrapper for the official enterprise WeChat message audit native SDK.
- Added basic tests, NuGet package metadata, README, and license files.
