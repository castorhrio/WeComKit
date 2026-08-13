namespace WeComKit.Core;

/// <summary>
/// 回调验证失败原因
/// </summary>
public enum WeComCallbackValidationFailure
{
    /// <summary>签名不匹配</summary>
    InvalidSignature,

    /// <summary>Timestamp 非法或超出允许的时间偏差</summary>
    InvalidTimestamp,

    /// <summary>检测到重放请求（nonce 已被消费）</summary>
    ReplayRejected
}

/// <summary>
/// 回调验证异常。
///
/// 用于 <see cref="WeComMessageCrypt.VerifyAndDecryptAsync"/> 流程中各安全门禁的统一失败信号，
/// AES 解密本身的失败仍抛 <see cref="System.Security.Cryptography.CryptographicException"/>。
/// </summary>
public sealed class WeComCallbackValidationException : Exception
{
    /// <summary>失败的门禁</summary>
    public WeComCallbackValidationFailure Failure { get; }

    public WeComCallbackValidationException(WeComCallbackValidationFailure failure)
        : base($"回调验证失败：{failure}")
    {
        Failure = failure;
    }

    public WeComCallbackValidationException(WeComCallbackValidationFailure failure, Exception innerException)
        : base($"回调验证失败：{failure}", innerException)
    {
        Failure = failure;
    }
}
