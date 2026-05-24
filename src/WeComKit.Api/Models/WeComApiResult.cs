using System.Text.Json.Serialization;

namespace WeComKit.Api.Models;

/// <summary>
/// 企业微信 API 通用响应基类
/// </summary>
public class WeComApiResult
{
    /// <summary>错误码，0 表示成功</summary>
    [JsonPropertyName("errcode")]
    public int ErrCode { get; set; }

    /// <summary>错误信息</summary>
    [JsonPropertyName("errmsg")]
    public string ErrMsg { get; set; } = string.Empty;

    /// <summary>检查响应是否成功，非 0 时抛异常</summary>
    public void EnsureSuccess()
    {
        if (ErrCode != 0)
            throw new WeComApiException(ErrCode, ErrMsg);
    }
}
