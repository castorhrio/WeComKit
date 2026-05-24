using System.Text.Json.Serialization;

namespace WeComKit.Api.Models;

internal sealed class WeComTokenResponse : WeComApiResult
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}
