using WeComKit.MsgAudit.Models;

namespace WeComKit.MsgAudit;

/// <summary>
/// 会话存档数据源的内部抽象。
///
/// <see cref="MsgAuditSessionReader"/> 依赖本接口而非具体 <see cref="WeComFinanceSdk"/>，
/// 以便单元测试注入 fake 数据源（原生 SDK 依赖 P/Invoke，无法在测试中直接构造）。
/// 生产环境通过 <see cref="WeComFinanceSdk"/> 的显式实现注入。
///
/// 本接口为 internal，不作为公共 API；如未来确需第三方自定义数据源再考虑公开。
/// </summary>
internal interface IMsgAuditChatDataSource
{
    /// <summary>拉取一批会话消息（自指定 seq 起，最多 limit 条）。</summary>
    Task<ChatDataResponse> GetChatDataAsync(ulong seq, uint limit, CancellationToken cancellationToken = default);

    /// <summary>使用已 RSA 解密的对称密钥解密单条消息。</summary>
    Task<ChatRecord> DecryptChatRecordAsync(string decryptedKey, string encryptChatMsg, CancellationToken cancellationToken = default);
}
