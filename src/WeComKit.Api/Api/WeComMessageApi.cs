using WeComKit.Api.Http;
using WeComKit.Api.Models;

namespace WeComKit.Api;

/// <summary>
/// 企业微信消息推送 API
/// 支持文本、图片、语音、视频、文件、文本卡片、图文、Markdown 消息类型
/// </summary>
public class WeComMessageApi
{
    private readonly WeComHttpClient _client;
    private readonly WeComOptions _options;

    public WeComMessageApi(WeComHttpClient client, WeComOptions options)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// 发送文本消息
    /// </summary>
    public Task<MessageSendResponse> SendTextAsync(TextMessageRequest request, CancellationToken ct = default)
    {
        SetAgentId(request);
        return _client.PostAsync<MessageSendResponse>("/cgi-bin/message/send", request, ct);
    }

    /// <summary>
    /// 发送图片消息
    /// </summary>
    public Task<MessageSendResponse> SendImageAsync(ImageMessageRequest request, CancellationToken ct = default)
    {
        SetAgentId(request);
        return _client.PostAsync<MessageSendResponse>("/cgi-bin/message/send", request, ct);
    }

    /// <summary>
    /// 发送语音消息
    /// </summary>
    public Task<MessageSendResponse> SendVoiceAsync(VoiceMessageRequest request, CancellationToken ct = default)
    {
        SetAgentId(request);
        return _client.PostAsync<MessageSendResponse>("/cgi-bin/message/send", request, ct);
    }

    /// <summary>
    /// 发送视频消息
    /// </summary>
    public Task<MessageSendResponse> SendVideoAsync(VideoMessageRequest request, CancellationToken ct = default)
    {
        SetAgentId(request);
        return _client.PostAsync<MessageSendResponse>("/cgi-bin/message/send", request, ct);
    }

    /// <summary>
    /// 发送文件消息
    /// </summary>
    public Task<MessageSendResponse> SendFileAsync(FileMessageRequest request, CancellationToken ct = default)
    {
        SetAgentId(request);
        return _client.PostAsync<MessageSendResponse>("/cgi-bin/message/send", request, ct);
    }

    /// <summary>
    /// 发送文本卡片消息
    /// </summary>
    public Task<MessageSendResponse> SendTextCardAsync(TextCardMessageRequest request, CancellationToken ct = default)
    {
        SetAgentId(request);
        return _client.PostAsync<MessageSendResponse>("/cgi-bin/message/send", request, ct);
    }

    /// <summary>
    /// 发送图文消息
    /// </summary>
    public Task<MessageSendResponse> SendNewsAsync(NewsMessageRequest request, CancellationToken ct = default)
    {
        if (request.News.Articles.Count == 0)
            throw new ArgumentException("图文消息至少包含一篇文章");

        SetAgentId(request);
        return _client.PostAsync<MessageSendResponse>("/cgi-bin/message/send", request, ct);
    }

    /// <summary>
    /// 发送 Markdown 消息
    /// </summary>
    public Task<MessageSendResponse> SendMarkdownAsync(MarkdownMessageRequest request, CancellationToken ct = default)
    {
        SetAgentId(request);
        return _client.PostAsync<MessageSendResponse>("/cgi-bin/message/send", request, ct);
    }

    /// <summary>
    /// 发送模板卡片消息。
    /// </summary>
    public Task<MessageSendResponse> SendTemplateCardAsync(TemplateCardMessageRequest request, CancellationToken ct = default)
    {
        SetAgentId(request);
        return _client.PostAsync<MessageSendResponse>("/cgi-bin/message/send", request, ct);
    }

    #region 内部方法

    private void SetAgentId(MessageSendRequest request)
    {
        if (request.AgentId is null)
            request.AgentId = int.TryParse(_options.AgentId, out var id) ? id : null;
    }

    #endregion
}
