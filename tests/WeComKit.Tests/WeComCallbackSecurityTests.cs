using System.Security.Cryptography;
using System.Text;
using WeComKit.Core;

namespace WeComKit.Tests;

/// <summary>
/// 回调安全相关测试：签名比较、解密负面用例、VerifyAndDecryptAsync 完整管道。
/// </summary>
public class WeComCallbackSecurityTests
{
    private const string Token = "QDG6eK";
    private const string CorpId = "wx5823bf96d3bd56c7";
    private const string PlainPayload = "<xml><ToUserName><![CDATA[wx5823bf96d3bd56c7]]></ToUserName><Encrypt><![CDATA[something]]></Encrypt></xml>";

    [Fact]
    public void VerifySignature_AcceptsCorrectSignature()
    {
        var crypt = NewCrypt();
        const string ts = "1710000000";
        const string nonce = "nonce-abc";
        const string data = "encrypted-payload";
        var sig = ComputeSig(Token, ts, nonce, data);

        Assert.True(crypt.VerifySignature(sig, ts, nonce, data));
    }

    [Fact]
    public void VerifySignature_RejectsModifiedTimestamp()
    {
        var crypt = NewCrypt();
        var sig = ComputeSig(Token, "1710000000", "nonce", "data");
        // 验证时把 timestamp 换掉
        Assert.False(crypt.VerifySignature(sig, "1710000001", "nonce", "data"));
    }

    [Fact]
    public void VerifySignature_RejectsModifiedNonce()
    {
        var crypt = NewCrypt();
        var sig = ComputeSig(Token, "1710000000", "nonce", "data");
        Assert.False(crypt.VerifySignature(sig, "1710000000", "TAMPERED", "data"));
    }

    [Fact]
    public void VerifySignature_RejectsOneCharTamperedSignature()
    {
        var crypt = NewCrypt();
        var sig = ComputeSig(Token, "1710000000", "nonce", "data");
        // 翻转最后一位字符（保持十六进制合法性）
        var lastChar = sig[^1];
        var replacement = lastChar == '0' ? '1' : '0';
        var tampered = sig[..^1] + replacement;

        Assert.False(crypt.VerifySignature(tampered, "1710000000", "nonce", "data"));
    }

    [Fact]
    public void VerifySignature_RejectsModifiedCiphertext()
    {
        var crypt = NewCrypt();
        const string ts = "1710000000";
        const string nonce = "nonce";
        var sig = ComputeSig(Token, ts, nonce, "data-A");
        Assert.False(crypt.VerifySignature(sig, ts, nonce, "data-B"));
    }

    [Fact]
    public void VerifySignature_RejectsEmptySignature()
    {
        var crypt = NewCrypt();
        Assert.False(crypt.VerifySignature("", "1710000000", "nonce", "data"));
    }

    [Fact]
    public void VerifySignature_RejectsOddLengthSignature()
    {
        var crypt = NewCrypt();
        // 41 字符（奇数）→ 非法十六进制长度
        Assert.False(crypt.VerifySignature("abc", "1710000000", "nonce", "data"));
    }

    [Fact]
    public void VerifySignature_RejectsNonHexSignature()
    {
        var crypt = NewCrypt();
        // 长度合法但含非十六进制字符
        var badSig = new string('z', 40);
        Assert.False(crypt.VerifySignature(badSig, "1710000000", "nonce", "data"));
    }

    [Fact]
    public void DecryptMsg_RejectsInvalidBase64()
    {
        var crypt = NewCrypt();
        // 含空格 → 非法 Base64
        Assert.Throws<CryptographicException>(() => crypt.DecryptMsg("!!!not-base64!!!"));
    }

    [Fact]
    public void DecryptMsg_RejectsTruncatedCiphertext()
    {
        var crypt = NewCrypt();
        // 合法 Base64 但长度不足以构成 AES 块
        Assert.Throws<CryptographicException>(() => crypt.DecryptMsg(Convert.ToBase64String(new byte[5])));
    }

    [Fact]
    public void DecryptMsg_RejectsInvalidPadding()
    {
        var crypt = NewCrypt();
        // 32 字节随机数据（合法 AES 块对齐，但 padding 几乎必然非法）
        var garbage = new byte[32];
        RandomNumberGenerator.Fill(garbage);
        Assert.Throws<CryptographicException>(() => crypt.DecryptMsg(Convert.ToBase64String(garbage)));
    }

    [Fact]
    public void Constructor_RejectsInvalidEncodingAesKey()
    {
        Assert.Throws<ArgumentException>(() => new WeComMessageCrypt(Token, "too-short", CorpId));
    }

    #region VerifyAndDecryptAsync 完整管道

    [Fact]
    public async Task VerifyAndDecryptAsync_PassesAllGates_ReturnsPlaintext()
    {
        var crypt = NewCrypt();
        var encrypted = crypt.EncryptMsg(PlainPayload);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var sig = ComputeSig(Token, now.ToString(), "nonce-1", encrypted);

        var options = new WeComCallbackSecurityOptions();
        var result = await crypt.VerifyAndDecryptAsync(sig, now.ToString(), "nonce-1", encrypted, options);

        Assert.Equal(PlainPayload, result);
    }

    [Fact]
    public async Task VerifyAndDecryptAsync_BadSignature_ThrowsInvalidSignature()
    {
        var crypt = NewCrypt();
        var encrypted = crypt.EncryptMsg(PlainPayload);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var ex = await Assert.ThrowsAsync<WeComCallbackValidationException>(() =>
            crypt.VerifyAndDecryptAsync("deadbeef".PadRight(40, '0'), now.ToString(), "nonce", encrypted,
                new WeComCallbackSecurityOptions()));
        Assert.Equal(WeComCallbackValidationFailure.InvalidSignature, ex.Failure);
    }

    [Fact]
    public async Task VerifyAndDecryptAsync_ExpiredTimestamp_ThrowsInvalidTimestamp()
    {
        var crypt = NewCrypt();
        var encrypted = crypt.EncryptMsg(PlainPayload);
        // 1 年前的时间戳
        var oldTs = DateTimeOffset.UtcNow.AddDays(-2).ToUnixTimeSeconds();
        var sig = ComputeSig(Token, oldTs.ToString(), "nonce", encrypted);

        var ex = await Assert.ThrowsAsync<WeComCallbackValidationException>(() =>
            crypt.VerifyAndDecryptAsync(sig, oldTs.ToString(), "nonce", encrypted,
                new WeComCallbackSecurityOptions()));
        Assert.Equal(WeComCallbackValidationFailure.InvalidTimestamp, ex.Failure);
    }

    [Fact]
    public async Task VerifyAndDecryptAsync_NonNumericTimestamp_ThrowsInvalidTimestamp()
    {
        var crypt = NewCrypt();
        var encrypted = crypt.EncryptMsg(PlainPayload);
        var sig = ComputeSig(Token, "abc", "nonce", encrypted);

        var ex = await Assert.ThrowsAsync<WeComCallbackValidationException>(() =>
            crypt.VerifyAndDecryptAsync(sig, "abc", "nonce", encrypted,
                new WeComCallbackSecurityOptions()));
        Assert.Equal(WeComCallbackValidationFailure.InvalidTimestamp, ex.Failure);
    }

    [Fact]
    public async Task VerifyAndDecryptAsync_ReplayRejected_ThrowsReplayRejected()
    {
        var crypt = NewCrypt();
        var encrypted = crypt.EncryptMsg(PlainPayload);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var sig = ComputeSig(Token, now.ToString(), "nonce-1", encrypted);

        var protector = new FakeReplayProtector(accept: false);
        var ex = await Assert.ThrowsAsync<WeComCallbackValidationException>(() =>
            crypt.VerifyAndDecryptAsync(sig, now.ToString(), "nonce-1", encrypted,
                new WeComCallbackSecurityOptions(), protector));
        Assert.Equal(WeComCallbackValidationFailure.ReplayRejected, ex.Failure);
    }

    [Fact]
    public async Task VerifyAndDecryptAsync_NullProtector_SkipsReplayCheck()
    {
        var crypt = NewCrypt();
        var encrypted = crypt.EncryptMsg(PlainPayload);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var sig = ComputeSig(Token, now.ToString(), "nonce-1", encrypted);

        // 不传 protector → 不做 replay 校验，流程通过
        var result = await crypt.VerifyAndDecryptAsync(sig, now.ToString(), "nonce-1", encrypted,
            new WeComCallbackSecurityOptions(), replayProtector: null);
        Assert.Equal(PlainPayload, result);
    }

    [Fact]
    public async Task VerifyAndDecryptAsync_PassesParsedTimestampToProtector()
    {
        var crypt = NewCrypt();
        var encrypted = crypt.EncryptMsg(PlainPayload);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var sig = ComputeSig(Token, now.ToString(), "nonce-1", encrypted);

        var protector = new FakeReplayProtector(accept: true);
        await crypt.VerifyAndDecryptAsync(sig, now.ToString(), "nonce-1", encrypted,
            new WeComCallbackSecurityOptions(), protector);

        Assert.Equal(now, protector.LastTimestamp);
        Assert.Equal("nonce-1", protector.LastNonce);
    }

    #endregion

    #region helpers

    private static WeComMessageCrypt NewCrypt() => new(Token, CreateEncodingAesKey(), CorpId);

    internal static string CreateEncodingAesKey()
    {
        var key = Enumerable.Range(1, 32).Select(static i => (byte)i).ToArray();
        return Convert.ToBase64String(key).TrimEnd('=');
    }

    private static string ComputeSig(string token, string ts, string nonce, string? data = null)
    {
        var arr = data is null ? new[] { token, ts, nonce } : new[] { token, ts, nonce, data };
        Array.Sort(arr, StringComparer.Ordinal);
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(string.Concat(arr)));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    internal sealed class FakeReplayProtector : ICallbackReplayProtector
    {
        private readonly bool _accept;
        public string? LastNonce { get; private set; }
        public long LastTimestamp { get; private set; }

        public FakeReplayProtector(bool accept) => _accept = accept;

        public Task<bool> TryAcceptAsync(string nonce, long unixTimestamp, CancellationToken cancellationToken = default)
        {
            LastNonce = nonce;
            LastTimestamp = unixTimestamp;
            return Task.FromResult(_accept);
        }
    }

    #endregion
}
