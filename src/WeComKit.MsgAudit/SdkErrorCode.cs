namespace WeComKit.MsgAudit;

/// <summary>
/// 企业微信会话存档 SDK 原生错误码
/// </summary>
public enum SdkErrorCode
{
    /// <summary>成功</summary>
    Success = 0,

    /// <summary>参数错误</summary>
    InvalidParameter = 10000,

    /// <summary>网络错误</summary>
    NetworkError = 10001,

    /// <summary>数据解析失败</summary>
    DataParseError = 10002,

    /// <summary>系统内部错误</summary>
    InternalError = 10003,

    /// <summary>媒体数据为空</summary>
    MediaDataEmpty = 10004,

    /// <summary>媒体数据未就绪</summary>
    MediaDataNotReady = 10005,

    /// <summary>SDK 未初始化</summary>
    SdkNotInitialized = 10006,

    /// <summary>消息体超长</summary>
    MessageTooLong = 10007,

    /// <summary>请求频率超限</summary>
    RateLimited = 10008,

    /// <summary>域名校验失败</summary>
    DomainVerifyError = 10009,

    /// <summary>IP 白名单限制</summary>
    IpWhitelistError = 10010,
}

/// <summary>
/// 错误码辅助方法
/// </summary>
public static class SdkErrorCodeExtensions
{
    /// <summary>
    /// 获取错误码的描述信息
    /// </summary>
    public static string GetDescription(this SdkErrorCode code) => code switch
    {
        SdkErrorCode.Success => "成功",
        SdkErrorCode.InvalidParameter => "参数错误",
        SdkErrorCode.NetworkError => "网络错误",
        SdkErrorCode.DataParseError => "数据解析失败",
        SdkErrorCode.InternalError => "系统内部错误",
        SdkErrorCode.MediaDataEmpty => "媒体数据为空",
        SdkErrorCode.MediaDataNotReady => "媒体数据未就绪",
        SdkErrorCode.SdkNotInitialized => "SDK 未初始化",
        SdkErrorCode.MessageTooLong => "消息体超长",
        SdkErrorCode.RateLimited => "请求频率超限",
        SdkErrorCode.DomainVerifyError => "域名校验失败",
        SdkErrorCode.IpWhitelistError => "IP 白名单限制",
        _ => $"未知错误码: {(int)code}",
    };

    /// <summary>
    /// 将 int 错误码强制转换为枚举（未知值也会保留原值）
    /// </summary>
    public static SdkErrorCode FromInt(int code) => (SdkErrorCode)code;
}
