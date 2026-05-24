using WeComKit.MsgAudit;

namespace WeComKit.Tests;

public class WeComFinanceSdkTests
{
    [Fact]
    public void CheckAvailability_DoesNotThrowWhenNativeSdkIsMissing()
    {
        var result = WeComFinanceSdk.CheckAvailability();

        Assert.NotNull(result.MissingFiles);
    }
}
