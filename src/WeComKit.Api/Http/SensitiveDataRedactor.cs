using System.Net;
using System.Text.RegularExpressions;

namespace WeComKit.Api.Http;

/// <summary>
/// 敏感数据脱敏工具
///
/// 任何日志 / 异常均不得直接输出 access_token、corpsecret、suite_secret、suite_ticket、
/// AES key、RSA 私钥、Authorization Header 等敏感值。本类提供统一的脱敏实现。
/// </summary>
public static class SensitiveDataRedactor
{
    /// <summary>统一脱敏占位符</summary>
    public const string Mask = "***REDACTED***";

    // access_token=xxx / suite_access_token=xxx / secret=xxx / corpsecret=xxx / encodingAESKey=xxx 等查询参数
    private static readonly Regex SensitiveQueryRegex = new(
        @"(?<key>access_token|suite_access_token|secret|corpsecret|suite_secret|suite_ticket|encodingaeskey)=" +
        @"[^&\s]*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // 授权头（Basic/Bearer/任意）
    private static readonly Regex AuthorizationHeaderRegex = new(
        @"(Authorization\s*:\s*)([^\r\n]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // PEM 私钥块
    private static readonly Regex PemBlockRegex = new(
        @"-----BEGIN [A-Z ]*PRIVATE KEY-----.*?-----END [A-Z ]*PRIVATE KEY-----",
        RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>
    /// 对字符串中的敏感片段进行脱敏。
    /// 处理 PEM 私钥块、Authorization 头，以及查询参数形式的 token/secret。
    /// </summary>
    public static string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var redacted = PemBlockRegex.Replace(text, "***REDACTED PRIVATE KEY***");
        redacted = AuthorizationHeaderRegex.Replace(redacted, "${1}" + Mask);
        redacted = SensitiveQueryRegex.Replace(redacted, "${key}=" + Mask);
        return redacted;
    }

    /// <summary>
    /// 脱敏 URL 中的敏感查询参数（access_token / suite_access_token / secret / corpsecret 等）。
    /// </summary>
    public static string RedactUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return string.Empty;

        return SensitiveQueryRegex.Replace(url, "${key}=" + Mask);
    }

    /// <summary>
    /// 脱敏 URI 中的敏感查询参数。
    /// </summary>
    public static string RedactUrl(Uri? uri)
    {
        return uri is null ? string.Empty : RedactUrl(uri.ToString());
    }
}
