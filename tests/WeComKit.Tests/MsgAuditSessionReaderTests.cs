using System.Security.Cryptography;
using System.Text;
using WeComKit.Core;
using WeComKit.MsgAudit;
using WeComKit.MsgAudit.Models;

namespace WeComKit.Tests;

/// <summary>
/// MsgAuditSessionReader 的 checkpoint / at-least-once 语义测试。
/// 通过 internal IMsgAuditChatDataSource 注入 fake 数据源，绕过原生 SDK 依赖。
/// EncryptRandomKey 用真实 RSA 公钥加密，确保 reader 内部的 DecryptRsa 步骤可成功。
/// </summary>
public class MsgAuditSessionReaderTests
{
    private static readonly (string Pem, RSA PublicKey) RsaKey = GenerateRsa();

    [Fact]
    public async Task ReadAsync_AllSuccess_NumericGap_AdvancesToMaxSeq()
    {
        // seq 数值不连续（100,102,105）属正常，全部成功 → checkpoint = 105
        var source = FakeSource.NewBuilder()
            .ChatBatch(100, MakeFakeItem(100), MakeFakeItem(102), MakeFakeItem(105))
            .Build();

        var reader = new MsgAuditSessionReader(source, RsaKey.Pem);
        var result = await reader.ReadAsync(99);

        Assert.Equal(105, result.CheckpointSequence);
        Assert.Equal(3, result.Messages.Count);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public async Task ReadAsync_MidFailure_StopsCheckpointBeforeFailure()
    {
        // 100✓ 102✗ 105✓ → checkpoint = 100（105 仍返回，但不越过失败项）
        var source = FakeSource.NewBuilder()
            .ChatBatch(100, MakeFakeItem(100), MakeFakeItem(102, decryptFails: true), MakeFakeItem(105))
            .Build();

        var reader = new MsgAuditSessionReader(source, RsaKey.Pem);
        var result = await reader.ReadAsync(99);

        Assert.Equal(100, result.CheckpointSequence);
        Assert.Equal(2, result.Messages.Count);          // 100 与 105 都被返回
        Assert.Single(result.Failures);                  // 102 失败
        Assert.Equal(102, result.Failures[0].Seq);
    }

    [Fact]
    public async Task ReadAsync_OutOfOrderBatch_HandledBySeqSort()
    {
        // SDK 返回顺序乱（105✗ 100✓）→ 内部 OrderBy(seq) → 100✓ 105✗ → checkpoint = 100
        var source = FakeSource.NewBuilder()
            .ChatBatch(99, MakeFakeItem(105, decryptFails: true), MakeFakeItem(100))
            .Build();

        var reader = new MsgAuditSessionReader(source, RsaKey.Pem);
        var result = await reader.ReadAsync(99);

        Assert.Equal(100, result.CheckpointSequence);
        Assert.Single(result.Messages);                  // 100 成功
        Assert.Single(result.Failures);                  // 105 失败
    }

    [Fact]
    public async Task ReadAsync_FirstItemFails_CheckpointDoesNotAdvance()
    {
        var source = FakeSource.NewBuilder()
            .ChatBatch(99, MakeFakeItem(100, decryptFails: true), MakeFakeItem(101))
            .Build();

        var reader = new MsgAuditSessionReader(source, RsaKey.Pem);
        var result = await reader.ReadAsync(99);

        Assert.Equal(99, result.CheckpointSequence); // 不推进
        Assert.Single(result.Failures);
    }

    [Fact]
    public async Task ReadAsync_EmptyBatch_CheckpointUnchanged()
    {
        var source = FakeSource.NewBuilder().ChatBatch(99).Build();

        var reader = new MsgAuditSessionReader(source, RsaKey.Pem);
        var result = await reader.ReadAsync(99);

        Assert.Equal(99, result.CheckpointSequence);
        Assert.Empty(result.Messages);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public async Task ReadAsync_SdkError_ThrowsAndCheckpointNotReturned()
    {
        var source = new ThrowingChatDataSource();

        var reader = new MsgAuditSessionReader(source, RsaKey.Pem);

        // GetChatData 抛 WeComFinanceSdkException
        await Assert.ThrowsAsync<WeComFinanceSdkException>(() => reader.ReadAsync(99));
    }

    [Fact]
    public async Task ReadAsync_FailureAfterFailure_DoesNotAdvancePastFirstFailure()
    {
        // 100✓ 101✗ 102✗ → checkpoint = 100；两个失败都被记录
        var source = FakeSource.NewBuilder()
            .ChatBatch(99, MakeFakeItem(100), MakeFakeItem(101, decryptFails: true), MakeFakeItem(102, decryptFails: true))
            .Build();

        var reader = new MsgAuditSessionReader(source, RsaKey.Pem);
        var result = await reader.ReadAsync(99);

        Assert.Equal(100, result.CheckpointSequence);
        Assert.Single(result.Messages);
        Assert.Equal(2, result.Failures.Count);
    }

    [Fact]
    public void Constructor_RejectsNullSource()
    {
        Assert.Throws<ArgumentNullException>(() => new MsgAuditSessionReader((IMsgAuditChatDataSource)null!, RsaKey.Pem));
    }

    [Fact]
    public async Task ReadAsync_RejectsNegativeSequence()
    {
        // 负数 sequence 不能转成 ulong（会变成超大 cursor）
        var source = FakeSource.NewBuilder().ChatBatch(0).Build();
        var reader = new MsgAuditSessionReader(source, RsaKey.Pem);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => reader.ReadAsync(-1));
    }

    [Fact]
    public async Task ReadAsync_CancellationPropagates_NotRecordedAsFailure()
    {
        // 数据源在 DecryptChatRecordAsync 抛 OperationCanceledException → 必须向上传播，
        // 而不是记为 message failure 继续处理
        var source = new CancelingDataSource();
        var reader = new MsgAuditSessionReader(source, RsaKey.Pem);

        using var cts = new CancellationTokenSource();
        await Assert.ThrowsAsync<OperationCanceledException>(() => reader.ReadAsync(99, cancellationToken: cts.Token));
    }

    #region fakes

    /// <summary>
    /// 生成一对 RSA 密钥用于测试：(PEM 私钥, 公钥)。
    /// </summary>
    private static (string Pem, RSA PublicKey) GenerateRsa()
    {
        var rsa = RSA.Create(2048);
        var pem = new StringBuilder();
        pem.AppendLine("-----BEGIN RSA PRIVATE KEY-----");
        pem.AppendLine(Convert.ToBase64String(rsa.ExportRSAPrivateKey(), Base64FormattingOptions.InsertLineBreaks));
        pem.AppendLine("-----END RSA PRIVATE KEY-----");
        return (pem.ToString(), RSA.Create(rsa.ExportParameters(false)));
    }

    /// <summary>
    /// 构造一个 fake 项：encrypt_random_key 用公钥加密 "rk-{seq}"，使 reader 的 DecryptRsa 能还原出 seq。
    /// </summary>
    private static FakeItem MakeFakeItem(long seq, bool decryptFails = false)
    {
        var keyBytes = Encoding.UTF8.GetBytes($"rk-{seq}");
        var encryptedKey = Convert.ToBase64String(RsaKey.PublicKey.Encrypt(keyBytes, RSAEncryptionPadding.Pkcs1));
        return new FakeItem(seq, encryptedKey, decryptFails);
    }

    /// <summary>GetChatData 永远抛异常的数据源。</summary>
    private sealed class ThrowingChatDataSource : IMsgAuditChatDataSource
    {
        public Task<ChatDataResponse> GetChatDataAsync(ulong seq, uint limit, CancellationToken cancellationToken = default)
            => throw new WeComFinanceSdkException(10001, "GetChatData");

        public Task<ChatRecord> DecryptChatRecordAsync(string decryptedKey, string encryptChatMsg, CancellationToken cancellationToken = default)
            => throw new WeComFinanceSdkException(10002, "DecryptData");
    }

    /// <summary>
    /// 返回一条带合法 RSA 加密 key 的消息，但在 DecryptChatRecordAsync 抛 OperationCanceledException，
    /// 用于验证取消向上传播而非记为 failure。
    /// </summary>
    private sealed class CancelingDataSource : IMsgAuditChatDataSource
    {
        public Task<ChatDataResponse> GetChatDataAsync(ulong seq, uint limit, CancellationToken cancellationToken = default)
        {
            var keyBytes = Encoding.UTF8.GetBytes("rk-100");
            var encryptedKey = Convert.ToBase64String(RsaKey.PublicKey.Encrypt(keyBytes, RSAEncryptionPadding.Pkcs1));
            var response = new ChatDataResponse();
            response.ChatData.Add(new ChatDataItem { Seq = 100, MsgId = "m100", EncryptRandomKey = encryptedKey, EncryptChatMsg = "ok" });
            return Task.FromResult(response);
        }

        public Task<ChatRecord> DecryptChatRecordAsync(string decryptedKey, string encryptChatMsg, CancellationToken cancellationToken = default)
            => throw new OperationCanceledException(cancellationToken);
    }

    private sealed record FakeItem(long Seq, string EncryptedRandomKey, bool decryptFails = false);

    /// <summary>
    /// 可编排的 fake 数据源：按 seq 顺序返回预设批次，对 decryptFails 的项在 DecryptChatRecordAsync 抛异常。
    /// </summary>
    private sealed class FakeSource : IMsgAuditChatDataSource
    {
        private readonly Dictionary<ulong, List<FakeItem>> _batches;

        private FakeSource(Dictionary<ulong, List<FakeItem>> batches) => _batches = batches;

        public static FakeSourceBuilder NewBuilder() => new();

        public Task<ChatDataResponse> GetChatDataAsync(ulong seq, uint limit, CancellationToken cancellationToken = default)
        {
            // 单批次数据源：返回注册的唯一一批项（seq 过滤由 reader 逻辑负责，这里只供测试编排）
            var items = _batches.Count > 0
                ? _batches.Values.First()
                : new List<FakeItem>();

            var response = new ChatDataResponse();
            foreach (var item in items)
            {
                response.ChatData.Add(new ChatDataItem
                {
                    Seq = item.Seq,
                    MsgId = $"msgid-{item.Seq}",
                    EncryptRandomKey = item.EncryptedRandomKey,
                    EncryptChatMsg = item.decryptFails ? "WILL-FAIL" : "ok"
                });
            }
            return Task.FromResult(response);
        }

        public Task<ChatRecord> DecryptChatRecordAsync(string decryptedKey, string encryptChatMsg, CancellationToken cancellationToken = default)
        {
            if (encryptChatMsg == "WILL-FAIL")
                throw new WeComFinanceSdkException(10002, "DecryptData");

            // decryptedKey 由 reader 的 DecryptRsa 还原，形如 "rk-{seq}"
            var seq = long.Parse(decryptedKey.AsSpan("rk-".Length));
            return Task.FromResult(new ChatRecord { MsgId = $"msgid-{seq}", MsgType = "text" });
        }

        internal sealed class FakeSourceBuilder
        {
            private readonly Dictionary<ulong, List<FakeItem>> _batches = new();
            public FakeSourceBuilder ChatBatch(ulong fromSeq, params FakeItem[] items)
            {
                _batches[fromSeq] = items.ToList();
                return this;
            }
            public FakeSource Build() => new(_batches);
        }
    }

    #endregion
}
