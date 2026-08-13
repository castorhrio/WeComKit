using System.Text.Json;
using WeComKit.Core;
using WeComKit.MsgAudit.Models;

namespace WeComKit.MsgAudit;

/// <summary>
/// 会话存档高层读取器。
///
/// 把 GetChatData → RSA 解密 encrypt_random_key → DecryptData → 反序列化 的流程封装为一个高层 API。
/// Checkpoint 存储由调用方自行决定（文件 / Redis / 数据库），本类型不做绑定。
/// </summary>
/// <remarks>
/// 语义：<b>at-least-once</b>。当某条消息解密失败时，<see cref="MsgAuditReadResult.CheckpointSequence"/>
/// 不会越过它，下一轮会重新拉取并重试；这可能导致失败之后已成功处理的消息被重复处理。
/// </remarks>
public sealed class MsgAuditSessionReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly IMsgAuditChatDataSource _source;
    private readonly string _privateKeyPem;

    /// <summary>
    /// 构造读取器（生产用法）
    /// </summary>
    /// <param name="sdk">已初始化的企业微信会话存档 SDK</param>
    /// <param name="privateKeyPem">用于解密 encrypt_random_key 的 RSA 私钥（PEM）</param>
    public MsgAuditSessionReader(WeComFinanceSdk sdk, string privateKeyPem)
        : this((IMsgAuditChatDataSource)sdk, privateKeyPem) { }

    /// <summary>
    /// 构造读取器（内部测试用入口）
    /// </summary>
    internal MsgAuditSessionReader(IMsgAuditChatDataSource source, string privateKeyPem)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _privateKeyPem = privateKeyPem ?? throw new ArgumentNullException(nameof(privateKeyPem));
    }

    /// <summary>
    /// 读取并解密一批会话消息。
    /// </summary>
    /// <param name="sequence">起始 seq（上次返回的 <see cref="MsgAuditReadResult.CheckpointSequence"/>）</param>
    /// <param name="limit">本批拉取上限</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<MsgAuditReadResult> ReadAsync(long sequence, uint limit = 1000, CancellationToken cancellationToken = default)
    {
        // 负数 sequence 不能转换成 ulong（会变成超大 cursor），先拒绝
        if (sequence < 0)
            throw new ArgumentOutOfRangeException(nameof(sequence), "sequence 不能为负数");

        // 1. 拉取（SDK errcode != 0 时 GetChatDataAsync 内部会抛 WeComFinanceSdkException，不推进 cursor）
        var response = await _source.GetChatDataAsync((ulong)sequence, limit, cancellationToken).ConfigureAwait(false);

        var messages = new List<MsgAuditMessage>();
        var failures = new List<MsgAuditMessageFailure>();

        // 2. 按 seq 升序处理，使 cursor 正确性只依赖 seq 值而非 SDK 返回顺序
        var orderedItems = response.ChatData.OrderBy(x => x.Seq).ToList();

        // 3. 记录第一条失败前的最后一条成功 seq
        var checkpoint = sequence;
        var failed = false;

        foreach (var item in orderedItems)
        {
            // 失败之后仍尝试处理（供调用方观察），但不再推进 checkpoint
            if (await TryProcessAsync(item, cancellationToken).ConfigureAwait(false) is { } message)
            {
                messages.Add(message);
                if (!failed)
                    checkpoint = item.Seq;
            }
            else
            {
                failed = true;
                // failures 已在 TryProcessAsync 内填充
            }
        }

        return new MsgAuditReadResult
        {
            CheckpointSequence = checkpoint,
            Messages = messages,
            Failures = failures
        };

        // 局部函数：闭包捕获 failures 列表
        async Task<MsgAuditMessage?> TryProcessAsync(ChatDataItem item, CancellationToken ct)
        {
            try
            {
                // RSA 解密 encrypt_random_key
                var randomKey = WeComUtility.DecryptRsa(item.EncryptRandomKey, _privateKeyPem);
                // DecryptData 解密消息体
                var record = await _source.DecryptChatRecordAsync(randomKey, item.EncryptChatMsg, ct).ConfigureAwait(false);
                return new MsgAuditMessage { Seq = item.Seq, MsgId = item.MsgId, Record = record };
            }
            catch (OperationCanceledException)
            {
                // 取消不是“处理失败”，必须向上传播，不得记为 failure
                throw;
            }
            catch (Exception ex)
            {
                failures.Add(new MsgAuditMessageFailure
                {
                    Seq = item.Seq,
                    MsgId = item.MsgId,
                    Error = ex is WeComFinanceSdkException ? ex.Message : $"{ex.GetType().Name}: {ex.Message}"
                });
                return null;
            }
        }
    }
}
