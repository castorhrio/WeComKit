using System.Globalization;

namespace WeComKit.Core;

/// <summary>
/// 回调 Timestamp 校验器
///
/// 将时间源（<see cref="TimeProvider"/>）与校验逻辑解耦，便于测试注入虚拟时间。
/// </summary>
public sealed class WeComCallbackTimestampValidator
{
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// 创建校验器
    /// </summary>
    /// <param name="timeProvider">时间源，默认 <see cref="TimeProvider.System"/></param>
    public WeComCallbackTimestampValidator(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// 校验 Timestamp。
    ///
    /// 语义校验（合法 Unix 秒时间戳，且在 <see cref="DateTimeOffset"/> 可表示范围内）始终执行；
    /// 时钟偏差校验仅当 <see cref="WeComCallbackSecurityOptions.ValidateTimestamp"/> 为 true 时执行。
    /// 因此 <c>ValidateTimestamp = false</c> 表示“跳过时钟偏差校验”，
    /// 而不是“接受任意非数字输入”。
    /// </summary>
    /// <param name="timestamp">待校验的时间戳字符串</param>
    /// <param name="options">校验选项</param>
    /// <param name="unixTimestamp">解析成功时输出 Unix 秒；失败时为 0</param>
    /// <returns>是否通过校验</returns>
    public bool TryValidate(string? timestamp, WeComCallbackSecurityOptions options, out long unixTimestamp)
    {
        ArgumentNullException.ThrowIfNull(options);

        unixTimestamp = 0;

        // 1. 语法校验：必须是整数
        if (!long.TryParse(timestamp, NumberStyles.Integer, CultureInfo.InvariantCulture, out unixTimestamp))
            return false;

        // 2. 范围校验：long.MaxValue / long.MinValue 等超范围值不构成合法时间戳
        DateTimeOffset timestampUtc;
        try
        {
            timestampUtc = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }

        // 3. 时钟偏差校验（可关闭）
        if (!options.ValidateTimestamp)
            return true;

        var delta = _timeProvider.GetUtcNow() - timestampUtc;
        return delta.Duration() <= options.AllowedClockSkew;
    }
}
