using System.Net.Http.Json;
using WeComKit.Api.Http;
using WeComKit.Api.Models;

namespace WeComKit.Api;

/// <summary>
/// 企业微信机器人 webhook 和 response_url 消息发送 API。
/// </summary>
public class WeComBotApi
{
    private readonly HttpClient _http;
    private readonly WeComBotOptions _options;

    public WeComBotApi(HttpClient httpClient, WeComBotOptions options)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// 构建群机器人 webhook 地址。
    /// </summary>
    public static string BuildWebhookUrl(string webhookUrl, string webhookKey)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl))
            throw new ArgumentException("webhook 地址不能为空", nameof(webhookUrl));
        if (string.IsNullOrWhiteSpace(webhookKey))
            throw new ArgumentException("webhook key 不能为空", nameof(webhookKey));

        var separator = webhookUrl.Contains('?', StringComparison.Ordinal) ? "&key=" : "?key=";
        return $"{webhookUrl}{separator}{Uri.EscapeDataString(webhookKey)}";
    }

    /// <summary>
    /// 发送群机器人文本消息。
    /// </summary>
    public Task<WeComBotMessageResponse> SendWebhookTextAsync(
        string content,
        IEnumerable<string>? mentionedList = null,
        IEnumerable<string>? mentionedMobileList = null,
        CancellationToken ct = default)
    {
        var url = BuildWebhookUrl(_options.WebhookUrl, _options.WebhookKey);
        return SendTextAsync(url, content, mentionedList, mentionedMobileList, ct);
    }

    /// <summary>
    /// 发送群机器人 Markdown 消息。
    /// </summary>
    public Task<WeComBotMessageResponse> SendWebhookMarkdownAsync(string content, CancellationToken ct = default)
    {
        var url = BuildWebhookUrl(_options.WebhookUrl, _options.WebhookKey);
        return SendMarkdownAsync(url, content, ct);
    }

    /// <summary>
    /// 通过回调消息中的 response_url 回复文本消息。
    /// </summary>
    public Task<WeComBotMessageResponse> SendResponseTextAsync(
        string responseUrl,
        string content,
        IEnumerable<string>? mentionedList = null,
        IEnumerable<string>? mentionedMobileList = null,
        CancellationToken ct = default)
    {
        return SendTextAsync(responseUrl, content, mentionedList, mentionedMobileList, ct);
    }

    /// <summary>
    /// 通过回调消息中的 response_url 回复 Markdown 消息。
    /// </summary>
    public Task<WeComBotMessageResponse> SendResponseMarkdownAsync(string responseUrl, string content, CancellationToken ct = default)
    {
        return SendMarkdownAsync(responseUrl, content, ct);
    }

    private async Task<WeComBotMessageResponse> SendTextAsync(
        string url,
        string content,
        IEnumerable<string>? mentionedList,
        IEnumerable<string>? mentionedMobileList,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("发送地址不能为空", nameof(url));
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("消息内容不能为空", nameof(content));

        var request = new WeComBotTextMessageRequest
        {
            Text = new WeComBotTextMessage
            {
                Content = content,
                MentionedList = mentionedList?.ToList(),
                MentionedMobileList = mentionedMobileList?.ToList()
            }
        };

        using var response = await _http.PostAsJsonAsync(url, request, WeComHttpClient.JsonOptions, ct);
        return await WeComHttpClient.ReadApiResultAsync<WeComBotMessageResponse>(response, "POST bot message", url, ct);
    }

    private async Task<WeComBotMessageResponse> SendMarkdownAsync(string url, string content, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("发送地址不能为空", nameof(url));
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("消息内容不能为空", nameof(content));

        var request = new WeComBotMarkdownMessageRequest
        {
            Markdown = new WeComBotMarkdownMessage { Content = content }
        };

        using var response = await _http.PostAsJsonAsync(url, request, WeComHttpClient.JsonOptions, ct);
        return await WeComHttpClient.ReadApiResultAsync<WeComBotMessageResponse>(response, "POST bot message", url, ct);
    }
}
