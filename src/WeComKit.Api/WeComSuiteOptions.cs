namespace WeComKit.Api;

/// <summary>
/// 企业微信第三方应用 suite 配置。
/// </summary>
public class WeComSuiteOptions
{
    /// <summary>API 基础地址，默认 https://qyapi.weixin.qq.com。</summary>
    public string ApiUrl { get; set; } = "https://qyapi.weixin.qq.com";

    /// <summary>第三方应用 SuiteId。</summary>
    public string SuiteId { get; set; } = string.Empty;

    /// <summary>第三方应用 SuiteSecret。</summary>
    public string SuiteSecret { get; set; } = string.Empty;
}
