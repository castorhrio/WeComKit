using WeComKit.Api.Http;
using WeComKit.Api.Models;

namespace WeComKit.Api;

/// <summary>
/// 企业微信素材管理 API
/// 上传临时素材获取 media_id，用于消息推送中的图片、语音、视频、文件
/// </summary>
public class WeComMediaApi
{
    private readonly WeComHttpClient _client;

    public WeComMediaApi(WeComHttpClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// 上传临时素材
    /// </summary>
    /// <param name="type">素材类型：image / voice / video / file</param>
    /// <param name="fileName">文件名（含扩展名）</param>
    /// <param name="fileStream">文件流</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>包含 type / media_id / created_at 的响应</returns>
    public async Task<MediaUploadResponse> UploadAsync(string type, string fileName, Stream fileStream, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("素材类型不能为空", nameof(type));
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("文件名不能为空", nameof(fileName));
        if (fileStream is null)
            throw new ArgumentNullException(nameof(fileStream));

        return await _client.PostMultipartAsync<MediaUploadResponse>("/cgi-bin/media/upload", type, fileName, fileStream, ct);
    }

    /// <summary>
    /// 上传图片（永久，用于图文消息等场景）
    /// </summary>
    /// <param name="fileName">文件名</param>
    /// <param name="fileStream">文件流</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>包含 url 的响应</returns>
    public async Task<MediaUploadImgResponse> UploadImageAsync(string fileName, Stream fileStream, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("文件名不能为空", nameof(fileName));
        if (fileStream is null)
            throw new ArgumentNullException(nameof(fileStream));

        return await _client.PostMultipartAsync<MediaUploadImgResponse>("/cgi-bin/media/uploadimg", "image", fileName, fileStream, ct);
    }
}
