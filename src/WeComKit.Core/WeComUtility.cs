using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WeComKit.Core;

/// <summary>
/// 企业微信通用工具方法
/// </summary>
public static class WeComUtility
{
    private const string AlphaNumeric = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    #region 签名

    /// <summary>
    /// 验证消息签名（无消息体，3 参数模式）
    /// 用于微信公众号 GET 回调验证：排序 token, timestamp, nonce → SHA1
    /// </summary>
    /// <param name="token">回调 Token</param>
    /// <param name="signature">预期签名</param>
    /// <param name="timestamp">时间戳</param>
    /// <param name="nonce">随机数</param>
    /// <returns>签名是否匹配</returns>
    public static bool VerifySignature(string token, string signature, string timestamp, string nonce)
    {
        if (token is null || signature is null || timestamp is null || nonce is null)
            return false;

        var computed = WeComMessageCrypt.ComputeSignature(token, timestamp, nonce);
        return WeComSecurityCompare.FixedTimeHexEquals(computed, signature);
    }

    /// <summary>
    /// 生成 HMAC-SHA256 签名并进行 Base64 编码
    /// </summary>
    /// <param name="data">待签名数据</param>
    /// <param name="key">签名密钥</param>
    /// <returns>Base64 编码的签名</returns>
    public static string ComputeHmacSha256(string data, string key)
    {
        var hash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(key),
            Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hash);
    }

    #endregion

    #region 随机数

    /// <summary>
    /// 生成随机字符串（Unix 毫秒时间戳前缀 + 随机字符填充）
    /// </summary>
    /// <param name="length">总长度，默认 32</param>
    /// <returns>随机字符串</returns>
    public static string GenerateNonce(int length = 32)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var randomLen = Math.Max(0, length - timestamp.Length);

        var bytes = new byte[randomLen];
        RandomNumberGenerator.Fill(bytes);

        var randomPart = new char[randomLen];
        for (var i = 0; i < randomLen; i++)
            randomPart[i] = AlphaNumeric[bytes[i] % AlphaNumeric.Length];

        var result = timestamp + new string(randomPart);
        return result.Length > length ? result[..length] : result;
    }

    #endregion

    #region RSA 解密（会话存档）

    /// <summary>
    /// 使用 RSA 私钥解密 Base64 密文
    /// 用于企业微信会话存档 encrypt_random_key 解密
    /// </summary>
    /// <param name="encryptedBase64">Base64 编码的密文</param>
    /// <param name="privateKeyPem">
    /// PEM 格式 RSA 私钥。
    /// 支持完整 PEM（含 BEGIN/END 标记）或裸 Base64 私钥（自动补全头尾）
    /// </param>
    /// <returns>解密后的明文字符串（即消息对称密钥）</returns>
    public static string DecryptRsa(string encryptedBase64, string privateKeyPem)
    {
        if (string.IsNullOrWhiteSpace(encryptedBase64))
            throw new ArgumentNullException(nameof(encryptedBase64));
        if (string.IsNullOrWhiteSpace(privateKeyPem))
            throw new ArgumentNullException(nameof(privateKeyPem));

        try
        {
            var encrypted = Convert.FromBase64String(encryptedBase64);
            var pem = NormalizePem(privateKeyPem);

            using var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            var decrypted = rsa.Decrypt(encrypted, RSAEncryptionPadding.Pkcs1);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch (CryptographicException)
        {
            // 非法密文 / 错误 padding / 错误密钥：统一对外消息，避免泄漏 provider 原始异常文本。
            throw new CryptographicException("RSA 解密失败");
        }
        catch (Exception ex)
        {
            // 其它异常（Base64 解析、PEM 解析等）：同样不把内部消息拼进对外消息；保留为 InnerException 供排查。
            throw new CryptographicException("RSA 解密失败", ex);
        }
    }

    private static string NormalizePem(string key)
    {
        var trimmed = key.Trim();
        if (trimmed.Contains("-----BEGIN"))
            return trimmed;
        return $"-----BEGIN RSA PRIVATE KEY-----\n{trimmed}\n-----END RSA PRIVATE KEY-----";
    }

    #endregion

    #region 小程序数据解密

    /// <summary>
    /// 解密微信小程序加密数据并反序列化为指定类型
    /// AES-128-CBC + 标准 PKCS#7
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="encryptedData">Base64 加密数据（encryptedData）</param>
    /// <param name="sessionKey">Base64 编码的 session_key</param>
    /// <param name="iv">Base64 编码的初始向量</param>
    /// <returns>反序列化后的对象</returns>
    public static T DecryptMiniProgram<T>(string encryptedData, string sessionKey, string iv) where T : class
    {
        var json = DecryptMiniProgramToString(encryptedData, sessionKey, iv);
        var result = JsonSerializer.Deserialize<T>(json);
        if (result is null)
            throw new InvalidOperationException($"反序列化失败：目标类型 {typeof(T).Name}");
        return result;
    }

    /// <summary>
    /// 解密微信小程序加密数据并返回 JSON 字符串
    /// </summary>
    public static string DecryptMiniProgramToString(string encryptedData, string sessionKey, string iv)
    {
        if (string.IsNullOrEmpty(encryptedData))
            throw new ArgumentNullException(nameof(encryptedData));
        if (string.IsNullOrEmpty(sessionKey))
            throw new ArgumentNullException(nameof(sessionKey));
        if (string.IsNullOrEmpty(iv))
            throw new ArgumentNullException(nameof(iv));

        var keyBytes = Convert.FromBase64String(sessionKey);
        var ivBytes = Convert.FromBase64String(iv);
        var encryptedBytes = Convert.FromBase64String(encryptedData);

        using var aes = Aes.Create();
        aes.Key = keyBytes;
        aes.IV = ivBytes;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var decrypted = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
        return Encoding.UTF8.GetString(decrypted);
    }

    #endregion
}
