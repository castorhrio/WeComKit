using System.Security.Cryptography;
using System.Text;
using WeComKit.Core;

namespace WeComKit.Tests;

public class WeComMessageCryptTests
{
    [Fact]
    public void EncryptMsg_CanBeDecryptedBySameConfiguration()
    {
        var crypt = new WeComMessageCrypt("token", CreateEncodingAesKey(), "corp-id");
        const string plain = "<xml><Content>hello</Content></xml>";

        var encrypted = crypt.EncryptMsg(plain);
        var decrypted = crypt.DecryptMsg(encrypted);

        Assert.Equal(plain, decrypted);
    }

    [Fact]
    public void DecryptMsg_RejectsMismatchedCorpId()
    {
        var key = CreateEncodingAesKey();
        var sender = new WeComMessageCrypt("token", key, "corp-a");
        var receiver = new WeComMessageCrypt("token", key, "corp-b");

        var encrypted = sender.EncryptMsg("<xml />");

        Assert.Throws<CryptographicException>(() => receiver.DecryptMsg(encrypted));
    }

    [Fact]
    public void DecryptMsg_CorpIdMismatch_DoesNotLeakCorpIdInMessage()
    {
        // 安全：CorpId（配置侧）与攻击者可控的明文尾部 CorpId 都不得出现在异常文本中。
        var key = CreateEncodingAesKey();
        var sender = new WeComMessageCrypt("token", key, "secret-corp-A");
        var receiver = new WeComMessageCrypt("token", key, "secret-corp-B");

        var encrypted = sender.EncryptMsg("<xml />");

        var ex = Assert.Throws<CryptographicException>(() => receiver.DecryptMsg(encrypted));
        Assert.DoesNotContain("secret-corp-A", ex.Message);
        Assert.DoesNotContain("secret-corp-B", ex.Message);
    }

    [Fact]
    public void VerifySignature_ReturnsTrueForOfficialSortAndSha1Formula()
    {
        var crypt = new WeComMessageCrypt("token", CreateEncodingAesKey(), "corp-id");
        const string timestamp = "1710000000";
        const string nonce = "nonce";
        const string encrypted = "encrypted-payload";
        var signature = ComputeSignature("token", timestamp, nonce, encrypted);

        Assert.True(crypt.VerifySignature(signature, timestamp, nonce, encrypted));
    }

    private static string CreateEncodingAesKey()
    {
        var key = Enumerable.Range(1, 32).Select(static i => (byte)i).ToArray();
        return Convert.ToBase64String(key).TrimEnd('=');
    }

    private static string ComputeSignature(params string[] values)
    {
        Array.Sort(values, StringComparer.Ordinal);
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(string.Concat(values)));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    [Fact]
    public void DecryptRsa_InvalidCiphertext_WrapsAsGenericException_AndPreservesInner()
    {
        // 用真实 RSA 私钥（保证 ImportFromPem 成功，且 rsa.Decrypt 确实执行后失败）。
        var pem = GenerateRsaPem();
        // 合法 Base64 但不是有效 RSA 密文（长度不匹配 modulus 等）
        var badCiphertext = Convert.ToBase64String(new byte[16]);

        var ex = Assert.Throws<CryptographicException>(() => WeComUtility.DecryptRsa(badCiphertext, pem));

        Assert.Equal("RSA 解密失败", ex.Message);     // 对外消息统一，不泄漏 provider 文本
        Assert.NotNull(ex.InnerException);            // 原始异常保留为 InnerException
    }

    private static string GenerateRsaPem()
    {
        using var rsa = RSA.Create(2048);
        var sb = new StringBuilder();
        sb.AppendLine("-----BEGIN RSA PRIVATE KEY-----");
        sb.AppendLine(Convert.ToBase64String(rsa.ExportRSAPrivateKey(), Base64FormattingOptions.InsertLineBreaks));
        sb.AppendLine("-----END RSA PRIVATE KEY-----");
        return sb.ToString();
    }
}
