using WeComKit.Api.Http;

namespace WeComKit.Tests;

/// <summary>
/// SensitiveDataRedactor 测试。
/// </summary>
public class SensitiveDataRedactorTests
{
    [Fact]
    public void RedactUrl_StripsAccessToken()
    {
        var url = "https://qyapi.weixin.qq.com/cgi-bin/user/get?access_token=SECRET_TOKEN_123&userid=zhangsan";

        var redacted = SensitiveDataRedactor.RedactUrl(url);

        Assert.Contains("access_token=" + SensitiveDataRedactor.Mask, redacted);
        Assert.DoesNotContain("SECRET_TOKEN_123", redacted);
        Assert.Contains("userid=zhangsan", redacted); // 非敏感参数保留
    }

    [Fact]
    public void RedactUrl_StripsSuiteAccessToken()
    {
        var url = "https://example.test/cgi-bin/service/get_pre_auth_code?suite_access_token=SUITE_TK_XYZ";

        var redacted = SensitiveDataRedactor.RedactUrl(url);

        Assert.Contains("suite_access_token=" + SensitiveDataRedactor.Mask, redacted);
        Assert.DoesNotContain("SUITE_TK_XYZ", redacted);
    }

    [Fact]
    public void RedactUrl_StripsCorpSecret()
    {
        var url = "https://example.test/cgi-bin/gettoken?corpid=corp&corpsecret=MY_SECRET";

        var redacted = SensitiveDataRedactor.RedactUrl(url);

        Assert.Contains("corpsecret=" + SensitiveDataRedactor.Mask, redacted);
        Assert.DoesNotContain("MY_SECRET", redacted);
    }

    [Fact]
    public void Redact_MasksAuthorizationHeader()
    {
        var text = "Authorization: Bearer super-secret-bearer-token";

        var redacted = SensitiveDataRedactor.Redact(text);

        Assert.DoesNotContain("super-secret-bearer-token", redacted);
        Assert.Contains(SensitiveDataRedactor.Mask, redacted);
    }

    [Fact]
    public void Redact_MasksPemPrivateKey()
    {
        var pem = """
                  -----BEGIN RSA PRIVATE KEY-----
                  MIIEpAIBAAKCAQEA...secretkeydata...
                  -----END RSA PRIVATE KEY-----
                  """;

        var redacted = SensitiveDataRedactor.Redact(pem);

        Assert.DoesNotContain("secretkeydata", redacted);
        Assert.Contains("PRIVATE KEY", redacted);
    }

    [Fact]
    public void Redact_ReturnsEmptyForNullOrEmpty()
    {
        Assert.Equal(string.Empty, SensitiveDataRedactor.Redact(null));
        Assert.Equal(string.Empty, SensitiveDataRedactor.Redact(string.Empty));
        Assert.Equal(string.Empty, SensitiveDataRedactor.RedactUrl((string?)null));
    }

    [Fact]
    public void RedactUrl_LeavesCleanUrlUnchanged()
    {
        var url = "https://example.test/cgi-bin/department/list?id=10";

        Assert.Equal(url, SensitiveDataRedactor.RedactUrl(url));
    }
}
