using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace WeComKit.Core;

/// <summary>
/// 企业微信 / 微信公众号消息加解密
/// 实现与技术参考：
/// - 企业微信官方技术文档：https://developer.work.weixin.qq.com/document/path/90968
/// - 微信公众号官方技术文档：https://developers.weixin.qq.com/doc/offiaccount/Message_Management/Message_encryption_and_decryption_instructions.html
/// 
/// 消息体格式：16 字节随机数 | 4 字节网络序长度 | 消息明文 | CorpId/AppId
/// 加密算法：AES-256-CBC，IV 取 AES Key 前 16 字节，PKCS#7 块大小 32（兼容官方 C++ SDK）
/// </summary>
public class WeComMessageCrypt
{
    private const int BlockSize = 32;
    private const int RandomBytesLen = 16;
    private const int NetworkOrderLen = 4;

    private readonly string _token;
    private readonly byte[] _aesKey;
    private readonly string _corpId;

    /// <summary>
    /// 初始化消息加解密实例
    /// </summary>
    /// <param name="token">回调 Token（企业微信后台配置）</param>
    /// <param name="encodingAESKey">消息加解密密钥（43 位 Base64 字符串）</param>
    /// <param name="corpId">企业 ID（或公众号 AppId）</param>
    public WeComMessageCrypt(string token, string encodingAESKey, string corpId)
    {
        if (string.IsNullOrEmpty(token))
            throw new ArgumentNullException(nameof(token));
        if (string.IsNullOrEmpty(encodingAESKey))
            throw new ArgumentNullException(nameof(encodingAESKey));

        _token = token;
        _aesKey = DecodeAESKey(encodingAESKey);
        _corpId = corpId ?? string.Empty;
    }

    /// <summary>
    /// 验证消息签名（带消息体，4 参数模式）
    /// 用于 POST 回调验签：排序 token, timestamp, nonce, data → SHA1
    /// </summary>
    /// <param name="msgSignature">企业微信传入的 msg_signature</param>
    /// <param name="timestamp">时间戳</param>
    /// <param name="nonce">随机数</param>
    /// <param name="data">消息体密文（GET 时为 echostr，POST 时为 Encrypt 节点值）</param>
    /// <returns>签名是否匹配</returns>
    public bool VerifySignature(string msgSignature, string timestamp, string nonce, string data)
    {
        if (msgSignature is null || timestamp is null || nonce is null || data is null)
            return false;

        var computed = ComputeSignature(_token, timestamp, nonce, data);
        return WeComSecurityCompare.FixedTimeHexEquals(computed, msgSignature);
    }

    /// <summary>
    /// 解密回调消息
    /// 格式：16 字节随机数 | 4 字节网络序长度 | 消息明文 | CorpId
    /// </summary>
    /// <param name="encryptedMsg">Base64 编码的密文</param>
    /// <returns>解密后的消息明文</returns>
    public string DecryptMsg(string encryptedMsg)
    {
        if (string.IsNullOrEmpty(encryptedMsg))
            throw new ArgumentNullException(nameof(encryptedMsg));

        byte[] encrypted;
        try
        {
            encrypted = Convert.FromBase64String(encryptedMsg);
        }
        catch (FormatException)
        {
            throw new CryptographicException("Base64 解码失败");
        }
        var plain = AESDecrypt(encrypted, _aesKey);

        if (plain.Length < RandomBytesLen + NetworkOrderLen)
            throw new CryptographicException("解密后的数据长度不足");

        // 解析 4 字节网络序消息长度
        var lenBytes = new byte[NetworkOrderLen];
        Array.Copy(plain, RandomBytesLen, lenBytes, 0, NetworkOrderLen);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(lenBytes);
        var msgLen = BitConverter.ToInt32(lenBytes, 0);

        if (msgLen < 0 || msgLen > plain.Length - RandomBytesLen - NetworkOrderLen)
            throw new CryptographicException("消息长度无效");

        var content = Encoding.UTF8.GetString(plain, RandomBytesLen + NetworkOrderLen, msgLen);

        // 校验 CorpId / AppId
        if (!string.IsNullOrEmpty(_corpId))
        {
            var corpIdStart = RandomBytesLen + NetworkOrderLen + msgLen;
            var corpIdLen = plain.Length - corpIdStart;
            if (corpIdLen > 0)
            {
                var receivedCorpId = Encoding.UTF8.GetString(plain, corpIdStart, corpIdLen);
                if (!string.Equals(receivedCorpId, _corpId, StringComparison.Ordinal))
                    throw new CryptographicException("CorpId 不匹配");
            }
        }

        return content;
    }

    /// <summary>
    /// 加密回复消息
    /// 格式：16 字节随机数 | 4 字节网络序长度 | 消息明文 | CorpId
    /// </summary>
    /// <param name="plainMsg">明文回复消息（XML 格式）</param>
    /// <returns>Base64 编码的密文</returns>
    public string EncryptMsg(string plainMsg)
    {
        if (plainMsg is null)
            throw new ArgumentNullException(nameof(plainMsg));

        var randomBytes = new byte[RandomBytesLen];
        RandomNumberGenerator.Fill(randomBytes);

        var contentBytes = Encoding.UTF8.GetBytes(plainMsg);
        var lengthBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(contentBytes.Length));
        var corpIdBytes = Encoding.UTF8.GetBytes(_corpId);

        var combined = new byte[RandomBytesLen + NetworkOrderLen + contentBytes.Length + corpIdBytes.Length];
        Array.Copy(randomBytes, 0, combined, 0, RandomBytesLen);
        Array.Copy(lengthBytes, 0, combined, RandomBytesLen, NetworkOrderLen);
        Array.Copy(contentBytes, 0, combined, RandomBytesLen + NetworkOrderLen, contentBytes.Length);
        Array.Copy(corpIdBytes, 0, combined, RandomBytesLen + NetworkOrderLen + contentBytes.Length, corpIdBytes.Length);

        var encrypted = AESEncrypt(combined, _aesKey);
        return Convert.ToBase64String(encrypted);
    }

    /// <summary>
    /// 验签 + 解密一步完成
    /// </summary>
    public string VerifyAndDecrypt(string msgSignature, string timestamp, string nonce, string encryptedMsg)
    {
        if (!VerifySignature(msgSignature, timestamp, nonce, encryptedMsg))
            throw new UnauthorizedAccessException("签名验证失败");
        return DecryptMsg(encryptedMsg);
    }

    /// <summary>
    /// 完整的回调安全校验 + 解密流程（异步、可配置）。
    ///
    /// 执行顺序：签名 → Timestamp → Replay（可选）→ AES 解密。
    /// 各安全门禁的失败对应异常：
    /// <list type="bullet">
    /// <item>签名不匹配 → <see cref="WeComCallbackValidationException"/>（<see cref="WeComCallbackValidationFailure.InvalidSignature"/>）</item>
    /// <item>Timestamp 非法 → <see cref="WeComCallbackValidationException"/>（<see cref="WeComCallbackValidationFailure.InvalidTimestamp"/>）</item>
    /// <item>Replay 检测命中 → <see cref="WeComCallbackValidationException"/>（<see cref="WeComCallbackValidationFailure.ReplayRejected"/>）</item>
    /// <item>AES 解密失败 → <see cref="CryptographicException"/></item>
    /// </list>
    /// </summary>
    /// <param name="msgSignature">企业微信传入的 msg_signature</param>
    /// <param name="timestamp">时间戳</param>
    /// <param name="nonce">随机数</param>
    /// <param name="encryptedMsg">消息体密文</param>
    /// <param name="options">回调安全选项</param>
    /// <param name="replayProtector">可选的重放保护器，传入 null 跳过 Replay 校验</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>解密后的消息明文</returns>
    public async Task<string> VerifyAndDecryptAsync(
        string msgSignature,
        string timestamp,
        string nonce,
        string encryptedMsg,
        WeComCallbackSecurityOptions options,
        ICallbackReplayProtector? replayProtector = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        // 1. 签名校验（定长时间比较）
        if (!VerifySignature(msgSignature, timestamp, nonce, encryptedMsg))
            throw new WeComCallbackValidationException(WeComCallbackValidationFailure.InvalidSignature);

        // 2. Timestamp 校验（语法始终校验；时钟偏差按 options）
        var validator = new WeComCallbackTimestampValidator();
        if (!validator.TryValidate(timestamp, options, out var unixTimestamp))
            throw new WeComCallbackValidationException(WeComCallbackValidationFailure.InvalidTimestamp);

        // 3. Replay 校验（可选）
        if (replayProtector is not null)
        {
            bool accepted;
            try
            {
                accepted = await replayProtector.TryAcceptAsync(nonce, unixTimestamp, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new WeComCallbackValidationException(WeComCallbackValidationFailure.ReplayRejected, ex);
            }
            if (!accepted)
                throw new WeComCallbackValidationException(WeComCallbackValidationFailure.ReplayRejected);
        }

        // 4. AES 解密
        return DecryptMsg(encryptedMsg);
    }

    #region 签名计算

    internal static string ComputeSignature(string token, string timestamp, string nonce, string? data = null)
    {
        var arr = data is null
            ? new[] { token, timestamp, nonce }
            : new[] { token, timestamp, nonce, data };

        Array.Sort(arr, DictionaryComparer.Instance);

        var raw = string.Concat(arr);
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(raw));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// 字典序字符串比较器（与企业微信官方 SDK 排序语义一致）
    /// 按字符逐个比较 Unicode 码点，短字符串优先
    /// </summary>
    private sealed class DictionaryComparer : IComparer<string>
    {
        public static readonly DictionaryComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            if (x is null && y is null) return 0;
            if (x is null) return -1;
            if (y is null) return 1;

            var minLen = Math.Min(x.Length, y.Length);
            for (var i = 0; i < minLen; i++)
            {
                var cmp = x[i].CompareTo(y[i]);
                if (cmp != 0) return cmp;
            }
            return x.Length.CompareTo(y.Length);
        }
    }

    #endregion

    #region AES 加解密

    private static byte[] AESDecrypt(byte[] encrypted, byte[] aesKey)
    {
        var iv = new byte[16];
        Array.Copy(aesKey, iv, 16);

        using var aes = Aes.Create();
        aes.Key = aesKey;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;

        using var decryptor = aes.CreateDecryptor();
        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Write))
        {
            cs.Write(encrypted, 0, encrypted.Length);
        }

        return PKCS7Decode(ms.ToArray());
    }

    private static byte[] AESEncrypt(byte[] plain, byte[] aesKey)
    {
        var padded = PKCS7Encode(plain);
        var iv = new byte[16];
        Array.Copy(aesKey, iv, 16);

        using var aes = Aes.Create();
        aes.Key = aesKey;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;

        using var encryptor = aes.CreateEncryptor();
        return encryptor.TransformFinalBlock(padded, 0, padded.Length);
    }

    #endregion

    #region PKCS#7 填充（块大小 32，兼容官方 SDK）

    /// <summary>
    /// PKCS#7 编码，块大小为 32 字节（与企业微信官方 C++ SDK 保持一致）
    /// </summary>
    private static byte[] PKCS7Encode(byte[] input)
    {
        var padAmount = BlockSize - (input.Length % BlockSize);
        if (padAmount == 0)
            padAmount = BlockSize;

        var padded = new byte[input.Length + padAmount];
        Array.Copy(input, padded, input.Length);
        for (var i = input.Length; i < padded.Length; i++)
            padded[i] = (byte)padAmount;
        return padded;
    }

    /// <summary>
    /// PKCS#7 去填充（块大小 32，兼容官方 SDK）。
    ///
    /// 安全性：使用 constant-time 累加器校验填充，不在单个字节不匹配时提前返回，
    /// 且所有失败路径使用同一条错误信息，避免 padding oracle 信息泄漏。
    /// </summary>
    private static byte[] PKCS7Decode(byte[] input)
    {
        // 输入必须是 AES 块（16 字节）的整数倍；否则视为非法密文。
        if (input.Length == 0 || (input.Length % 16) != 0)
            throw new CryptographicException("解密失败");

        var padAmount = input[^1];
        var len = input.Length;

        // 累加所有“不匹配”，不在任何分支上提前返回；统一错误信息。
        var diff = 0;
        if (padAmount < 1 || padAmount > BlockSize) diff |= 1;
        if (padAmount > len) diff |= 1;

        // 固定遍历最后 BlockSize 个字节（padAmount 范围上限），用 mask 决定是否参与比较。
        // padAmount 非法时 padStart 会被钳制为 len（空范围），不会越界；
        // 即使比较出错也不会影响“已失败”的事实（diff 已置 1）。
        var padStart = padAmount <= BlockSize ? (len - padAmount) : len;
        var scanStart = len > BlockSize ? len - BlockSize : 0;
        for (var i = scanStart; i < len; i++)
        {
            // i 在 [padStart, len) 内才应等于 padAmount；否则该字节应被忽略。
            var inPad = i >= padStart ? 1 : 0;
            diff |= (input[i] ^ padAmount) & (-inPad); // mask: 0 或 0xFF
        }

        if (diff != 0)
            throw new CryptographicException("解密失败");

        var output = new byte[len - padAmount];
        Array.Copy(input, output, output.Length);
        return output;
    }

    #endregion

    #region AES Key 解码

    /// <summary>
    /// 将 43 位 Base64 EncodingAESKey 解码为 32 字节 AES Key
    /// </summary>
    private static byte[] DecodeAESKey(string encodingAESKey)
    {
        if (encodingAESKey.Length != 43)
            throw new ArgumentException($"EncodingAESKey 长度必须为 43，当前为 {encodingAESKey.Length}");

        try
        {
            var key = Convert.FromBase64String(encodingAESKey + "=");
            if (key.Length != 32)
                throw new ArgumentException($"AES Key 解码后必须为 32 字节，当前为 {key.Length}");
            return key;
        }
        catch (FormatException)
        {
            throw new ArgumentException("EncodingAESKey 格式无效：必须为合法 Base64 字符串");
        }
    }

    #endregion
}
