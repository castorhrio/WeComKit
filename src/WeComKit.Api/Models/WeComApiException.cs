using System.Net;
using WeComKit.Api.Http;

namespace WeComKit.Api.Models;

/// <summary>
/// 企业微信 API 调用异常
/// </summary>
public class WeComApiException : Exception
{
    /// <summary>错误码</summary>
    public int ErrorCode { get; }

    /// <summary>
    /// 错误信息。已在构造时脱敏（<see cref="SensitiveDataRedactor.Redact"/>），
    /// 可能与服务器原文不同。
    /// </summary>
    public string ErrorMessage { get; }

    /// <summary>
    /// 企业微信返回的错误信息（<see cref="ErrorMessage"/> 的别名，语义对齐文档）。
    /// <b>注意：</b>该值已在构造时通过 <see cref="SensitiveDataRedactor.Redact"/> 脱敏，
    /// 可能与服务器原文不同，<b>不得</b>当作未经处理的原始诊断数据使用。
    /// </summary>
    public string? WeComMessage => ErrorMessage;

    /// <summary>
    /// 请求路径。在构造时已通过 <see cref="SensitiveDataRedactor.RedactUrl"/> 脱敏，
    /// 不会泄漏 query 中的 access_token / secret 等敏感参数。
    /// </summary>
    public string? RequestPath { get; }

    /// <summary>HTTP 状态码（若适用）</summary>
    public HttpStatusCode? HttpStatus { get; }

    public WeComApiException(int errorCode, string errorMessage)
        : base($"企业微信 API 错误 [errcode={errorCode}]: {SensitiveDataRedactor.Redact(errorMessage)}")
    {
        ErrorCode = errorCode;
        ErrorMessage = SensitiveDataRedactor.Redact(errorMessage);
    }

    /// <summary>
    /// 构造 API 异常（携带请求路径与 HTTP 状态码）。
    /// <paramref name="requestPath"/> 与 <paramref name="errorMessage"/> 均会在内部脱敏后再存储，
    /// 确保不会泄漏 query 中的 access_token / secret 等敏感参数。
    /// </summary>
    public WeComApiException(int errorCode, string errorMessage, string? requestPath, HttpStatusCode? httpStatus = null)
        : base($"企业微信 API 错误 [errcode={errorCode}]: {SensitiveDataRedactor.Redact(errorMessage)}")
    {
        ErrorCode = errorCode;
        ErrorMessage = SensitiveDataRedactor.Redact(errorMessage);
        RequestPath = SensitiveDataRedactor.RedactUrl(requestPath);
        HttpStatus = httpStatus;
    }
}
