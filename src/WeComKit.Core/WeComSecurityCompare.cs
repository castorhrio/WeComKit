using System.Security.Cryptography;

namespace WeComKit.Core;

/// <summary>
/// 安全敏感值的定长时间比较工具
///
/// 用于回调签名校验：避免因提前返回导致的时间侧信道。
/// 签名本质上是字节，因此在字节层面比较；十六进制字符串先解码为字节再比较。
/// </summary>
internal static class WeComSecurityCompare
{
    /// <summary>
    /// 定长时间比较两个十六进制签名字符串。
    /// 长度不一致 / 非法十六进制 / null 均返回 false（签名长度属于公开信息，不构成侧信道）。
    /// </summary>
    public static bool FixedTimeHexEquals(string? expected, string? actual)
    {
        if (expected is null || actual is null) return false;
        if (expected.Length != actual.Length) return false;
        if (!TryDecodeHex(expected, out var expectedBytes)) return false;
        if (!TryDecodeHex(actual, out var actualBytes)) return false;
        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private static bool TryDecodeHex(string hex, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (hex.Length % 2 != 0) return false;

        var result = new byte[hex.Length / 2];
        for (var i = 0; i < result.Length; i++)
        {
            var hi = HexValue(hex[i * 2]);
            var lo = HexValue(hex[i * 2 + 1]);
            if (hi < 0 || lo < 0) return false;
            result[i] = (byte)((hi << 4) | lo);
        }

        bytes = result;
        return true;
    }

    private static int HexValue(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => -1
    };
}
