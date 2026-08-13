namespace WeComKit.MsgAudit;

/// <summary>
/// <see cref="MsgAuditSessionReader.ReadAsync"/> 的结果。
/// </summary>
public sealed class MsgAuditReadResult
{
    /// <summary>
    /// 下一次调用 <see cref="MsgAuditSessionReader.ReadAsync"/> 时应传入的 cursor。
    ///
    /// 语义：永远不跳过任何一条处理失败的消息。
    /// 规则：等于“第一条处理失败消息之前、最后一条成功处理消息的 seq”；
    /// 若本批全部成功，则为最大 seq；若为空批或第一条即失败，则等于传入的 sequence（不推进）。
    ///
    /// 数值上的 seq 不连续（gap）属于正常现象，不算失败，不会阻断 cursor 推进。
    /// cursor 的计算只依赖 seq 值本身，不依赖 SDK 返回数组的顺序（内部按 seq 升序处理）。
    ///
    /// 当某条消息解密失败时，本 cursor 不会越过它；下一轮会重新拉取并重试。
    /// 这意味着失败消息之后已成功处理的消息可能被重复处理，即 at-least-once 语义。
    /// </summary>
    public long CheckpointSequence { get; init; }

    /// <summary>本批成功处理的消息（包含失败消息之后仍尝试处理的部分）</summary>
    public IReadOnlyList<MsgAuditMessage> Messages { get; init; } = Array.Empty<MsgAuditMessage>();

    /// <summary>本批处理失败的消息</summary>
    public IReadOnlyList<MsgAuditMessageFailure> Failures { get; init; } = Array.Empty<MsgAuditMessageFailure>();
}
