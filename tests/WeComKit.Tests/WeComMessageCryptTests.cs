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
}
