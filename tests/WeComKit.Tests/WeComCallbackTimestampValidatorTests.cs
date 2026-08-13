using WeComKit.Core;

namespace WeComKit.Tests;

/// <summary>
/// WeComCallbackTimestampValidator 测试。
/// 覆盖：语法始终校验、时钟偏差可关闭、范围/溢出安全、负 skew 拒绝。
/// </summary>
public class WeComCallbackTimestampValidatorTests
{
    private static readonly DateTimeOffset FixedNow = new(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Validate_AcceptsCurrentTimestamp()
    {
        var validator = new WeComCallbackTimestampValidator(new FixedTimeProvider(FixedNow));
        var ts = FixedNow.ToUnixTimeSeconds().ToString();

        var ok = validator.TryValidate(ts, new WeComCallbackSecurityOptions(), out var parsed);

        Assert.True(ok);
        Assert.Equal(FixedNow.ToUnixTimeSeconds(), parsed);
    }

    [Fact]
    public void Validate_RejectsExpiredTimestamp_BeyondSkew()
    {
        var validator = new WeComCallbackTimestampValidator(new FixedTimeProvider(FixedNow));
        // 5 分钟默认 skew → 6 分钟前应拒绝
        var ts = FixedNow.AddMinutes(-6).ToUnixTimeSeconds().ToString();

        Assert.False(validator.TryValidate(ts, new WeComCallbackSecurityOptions(), out _));
    }

    [Fact]
    public void Validate_RejectsFutureTimestamp_BeyondSkew()
    {
        var validator = new WeComCallbackTimestampValidator(new FixedTimeProvider(FixedNow));
        var ts = FixedNow.AddMinutes(6).ToUnixTimeSeconds().ToString();

        Assert.False(validator.TryValidate(ts, new WeComCallbackSecurityOptions(), out _));
    }

    [Fact]
    public void Validate_AcceptsTimestamp_WithinSkew_Boundary()
    {
        var validator = new WeComCallbackTimestampValidator(new FixedTimeProvider(FixedNow));
        // 刚好在 skew 边界（5 分钟 - 1 秒）
        var ts = FixedNow.AddMinutes(-5).AddSeconds(1).ToUnixTimeSeconds().ToString();

        Assert.True(validator.TryValidate(ts, new WeComCallbackSecurityOptions(), out _));
    }

    [Fact]
    public void Validate_WithValidateTimestampFalse_AcceptsValidNumeric()
    {
        var validator = new WeComCallbackTimestampValidator(new FixedTimeProvider(FixedNow));
        // 远超 skew 的旧时间戳，但 ValidateTimestamp=false → 跳过偏差校验
        var ts = FixedNow.AddYears(-1).ToUnixTimeSeconds().ToString();
        var options = new WeComCallbackSecurityOptions { ValidateTimestamp = false };

        Assert.True(validator.TryValidate(ts, options, out _));
    }

    [Fact]
    public void Validate_WithValidateTimestampFalse_RejectsNonNumeric()
    {
        var validator = new WeComCallbackTimestampValidator(new FixedTimeProvider(FixedNow));
        var options = new WeComCallbackSecurityOptions { ValidateTimestamp = false };

        // 即使关闭了时钟校验，语法校验仍然生效
        Assert.False(validator.TryValidate("abc", options, out _));
        Assert.False(validator.TryValidate("", options, out _));
        Assert.False(validator.TryValidate(null, options, out _));
    }

    [Fact]
    public void Validate_RejectsLongMaxValue()
    {
        var validator = new WeComCallbackTimestampValidator(new FixedTimeProvider(FixedNow));

        // long.MaxValue 远超 DateTimeOffset 表示范围
        Assert.False(validator.TryValidate(long.MaxValue.ToString(), new WeComCallbackSecurityOptions(), out _));
    }

    [Fact]
    public void Validate_RejectsLongMinValue()
    {
        var validator = new WeComCallbackTimestampValidator(new FixedTimeProvider(FixedNow));

        Assert.False(validator.TryValidate(long.MinValue.ToString(), new WeComCallbackSecurityOptions(), out _));
    }

    [Fact]
    public void AllowedClockSkew_RejectsNegativeValue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WeComCallbackSecurityOptions { AllowedClockSkew = TimeSpan.FromMinutes(-1) });
    }

    [Fact]
    public void Validate_HonorsCustomSkew()
    {
        var validator = new WeComCallbackTimestampValidator(new FixedTimeProvider(FixedNow));
        var options = new WeComCallbackSecurityOptions { AllowedClockSkew = TimeSpan.FromSeconds(30) };

        // 2 分钟前 → 超过 30 秒 skew → 拒绝
        var ts = FixedNow.AddMinutes(-2).ToUnixTimeSeconds().ToString();
        Assert.False(validator.TryValidate(ts, options, out _));
    }

    /// <summary>
    /// 固定时间的 TimeProvider，便于确定性测试。
    /// </summary>
    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FixedTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
