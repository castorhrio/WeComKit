using System.Net;
using WeComKit.Api.Models;

namespace WeComKit.Tests;

/// <summary>
/// WeComApiException 测试，重点验证新 ctor 的 RequestPath 脱敏与向后兼容。
/// </summary>
public class WeComApiExceptionTests
{
    [Fact]
    public void OldConstructor_PreservesErrorCodeAndMessage()
    {
        var ex = new WeComApiException(42001, "access_token expired");

        Assert.Equal(42001, ex.ErrorCode);
        Assert.Equal("access_token expired", ex.ErrorMessage);
        Assert.Null(ex.RequestPath);
        Assert.Null(ex.HttpStatus);
        Assert.Contains("errcode=42001", ex.Message);
    }

    [Fact]
    public void NewConstructor_SetsPathAndStatus()
    {
        var ex = new WeComApiException(40014, "invalid token", "/cgi-bin/user/get", HttpStatusCode.Unauthorized);

        Assert.Equal(40014, ex.ErrorCode);
        Assert.Equal("/cgi-bin/user/get", ex.RequestPath);
        Assert.Equal(HttpStatusCode.Unauthorized, ex.HttpStatus);
    }

    [Fact]
    public void RequestPath_IsRedacted_StripsAccessToken()
    {
        var urlWithToken = "/cgi-bin/user/get?access_token=TOPSECRET&userid=zhangsan";

        var ex = new WeComApiException(1, "err", urlWithToken);

        Assert.DoesNotContain("TOPSECRET", ex.RequestPath!);
        Assert.Contains("access_token=***REDACTED***", ex.RequestPath!);
        Assert.Contains("userid=zhangsan", ex.RequestPath!);
    }

    [Fact]
    public void WeComMessage_AliasesErrorMessage()
    {
        var ex = new WeComApiException(1, "my message");

        Assert.Equal(ex.ErrorMessage, ex.WeComMessage);
    }

    [Fact]
    public void Message_DoesNotContainSecret_WhenPathHasSecret()
    {
        var urlWithSecret = "/cgi-bin/gettoken?corpsecret=SHHHH";
        var ex = new WeComApiException(1, "err", urlWithSecret);

        Assert.DoesNotContain("SHHHH", ex.Message);
    }
}
