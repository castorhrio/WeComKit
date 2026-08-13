using WeComKit.MsgAudit.Models;

namespace WeComKit.MsgAudit;

/// <summary>
/// 已解密、成功解析的单条会话消息
/// </summary>
public sealed class MsgAuditMessage
{
    /// <summary>消息 seq</summary>
    public long Seq { get; init; }

    /// <summary>消息 ID</summary>
    public string MsgId { get; init; } = string.Empty;

    /// <summary>解密后的消息体</summary>
    public ChatRecord Record { get; init; } = new();
}

/// <summary>
/// 处理失败的单条会话消息
/// </summary>
public sealed class MsgAuditMessageFailure
{
    /// <summary>消息 seq</summary>
    public long Seq { get; init; }

    /// <summary>消息 ID</summary>
    public string MsgId { get; init; } = string.Empty;

    /// <summary>失败原因</summary>
    public string Error { get; init; } = string.Empty;
}
