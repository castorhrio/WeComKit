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

    public WeComApiException(int errorCode, string errorMessage)
        : base($"企业微信 API 错误 [errcode={errorCode}]: {errorMessage}")
    {
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }
}
