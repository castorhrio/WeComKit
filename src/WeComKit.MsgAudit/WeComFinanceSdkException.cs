namespace WeComKit.MsgAudit;

/// <summary>
/// 企业微信会话存档 SDK 操作异常
/// 携带原生错误码和操作名称，方便上层精确处理
/// </summary>
public class WeComFinanceSdkException : Exception
{
    /// <summary>原生 SDK 错误码</summary>
    public int ErrorCode { get; }

    /// <summary>出错时的操作名称</summary>
    public string Operation { get; }

    /// <summary>
    /// 构造 SDK 异常
    /// </summary>
    /// <param name="errorCode">原生 SDK 错误码</param>
    /// <param name="operation">出错时的操作名称（如 GetChatData、DecryptData）</param>
    public WeComFinanceSdkException(int errorCode, string operation)
        : base(BuildMessage(errorCode, operation, null))
    {
        ErrorCode = errorCode;
        Operation = operation;
    }

    /// <summary>
    /// 构造 SDK 异常（带内部异常）
    /// </summary>
    /// <param name="errorCode">原生 SDK 错误码</param>
    /// <param name="operation">出错时的操作名称</param>
    /// <param name="innerException">内部异常（如 AccessViolationException）</param>
    public WeComFinanceSdkException(int errorCode, string operation, Exception innerException)
        : base(BuildMessage(errorCode, operation, innerException), innerException)
    {
        ErrorCode = errorCode;
        Operation = operation;
    }

    private static string BuildMessage(int errorCode, string operation, Exception? inner)
    {
        var code = (SdkErrorCode)errorCode;
        var desc = code.GetDescription();
        var suffix = inner is not null ? $"（原生异常: {inner.GetType().Name}）" : "";
        return $"{operation} 失败 — {desc} [错误码: {errorCode}]{suffix}";
    }
}
