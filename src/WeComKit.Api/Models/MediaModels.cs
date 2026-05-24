using System.Text.Json.Serialization;

namespace WeComKit.Api.Models;

/// <summary>
/// 临时素材上传响应。
/// </summary>
public class MediaUploadResponse : WeComApiResult
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("media_id")]
    public string MediaId { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = string.Empty;
}

/// <summary>
/// 永久图片上传响应。
/// </summary>
public class MediaUploadImgResponse : WeComApiResult
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}
