namespace WeComKit.Core;

/// <summary>
/// 回调重放保护扩展点。
///
/// Core 仅定义接口，默认不强制启用，也不绑定任何具体存储（Redis / MemoryCache / 数据库）。
/// 多实例部署环境下不应仅使用进程内缓存，应基于分布式存储实现本接口。
/// </summary>
public interface ICallbackReplayProtector
{
    /// <summary>
    /// 尝试接收（消费）一个 (nonce, timestamp) 组合。
    /// </summary>
    /// <param name="nonce">回调 nonce</param>
    /// <param name="unixTimestamp">已解析的 Unix 秒时间戳</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>
    /// <c>true</c>：本次请求可以继续；
    /// <c>false</c>：检测到重复请求 / replay，应拒绝。
    /// </returns>
    Task<bool> TryAcceptAsync(
        string nonce,
        long unixTimestamp,
        CancellationToken cancellationToken = default);
}
