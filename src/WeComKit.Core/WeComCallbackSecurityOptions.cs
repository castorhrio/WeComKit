namespace WeComKit.Core;

/// <summary>
/// 回调安全校验选项
/// </summary>
public sealed class WeComCallbackSecurityOptions
{
    private TimeSpan _allowedClockSkew = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 允许的时间偏差。默认 5 分钟。
    /// 不能为负数。
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">赋值为负数时抛出</exception>
    public TimeSpan AllowedClockSkew
    {
        get => _allowedClockSkew;
        set => _allowedClockSkew = value >= TimeSpan.Zero
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "AllowedClockSkew 不能为负数");
    }

    /// <summary>
    /// 是否校验 Timestamp 与当前时间的时间差。默认 true。
    /// 设为 false 时仍要求 Timestamp 为合法 Unix 秒时间戳（仅跳过时钟偏差校验）。
    /// </summary>
    public bool ValidateTimestamp { get; set; } = true;
}
