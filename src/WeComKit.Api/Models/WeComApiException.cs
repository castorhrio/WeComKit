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

    /// <summary>错误信息</summary>
    public string ErrorMessage { get; }

    /// <summary>
    /// 企业微信返回的原始错误信息（<see cref="ErrorMessage"/> 的别名，语义对齐文档）。
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
        : base($"企业微信 API 错误 [errcode={errorCode}]: {errorMessage}")
    {
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// 构造 API 异常（携带请求路径与 HTTP 状态码）。
    /// <paramref name="requestPath"/> 会在内部脱敏后再存储，确保不会泄漏 token / secret。
    /// </summary>
    public WeComApiException(int errorCode, string errorMessage, string? requestPath, HttpStatusCode? httpStatus = null)
        : base($"企业微信 API 错误 [errcode={errorCode}]: {errorMessage}")
    {
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        RequestPath = SensitiveDataRedactor.RedactUrl(requestPath);
        HttpStatus = httpStatus;
    }
}
