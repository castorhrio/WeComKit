using System.Collections.Concurrent;

namespace WeComKit.MsgAudit;

/// <summary>
/// 企业微信会话存档 SDK 工厂
///
/// 管理多个企业的 SDK 实例，避免重复 Init（Init 为网络调用，开销较大）。
/// 工厂拥有实例生命周期，调用方不得单独 Dispose 返回的 SDK。
///
/// 使用方式：
/// <code>
///   await using var factory = new WeComFinanceSdkFactory();
///   var sdk = await factory.GetSdkAsync(corpId, secret);
///   var data = await sdk.GetChatDataAsync(seq: 0, limit: 100);
/// </code>
///
/// 多企业场景：
/// <code>
///   var sdkA = await factory.GetSdkAsync(corpIdA, secretA);
///   var sdkB = await factory.GetSdkAsync(corpIdB, secretB);
///   // 两个实例独立运行，互不影响
/// </code>
/// </summary>
public class WeComFinanceSdkFactory : IAsyncDisposable, IDisposable
{
    private readonly ConcurrentDictionary<string, WeComFinanceSdk> _sdks = new();
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private volatile bool _disposed;

    /// <summary>当前缓存的 SDK 实例数</summary>
    public int Count => _sdks.Count;

    /// <summary>
    /// 获取或创建指定企业的 SDK 实例（已 Initialized）
    /// 同一个 (corpId, secret) 组合只创建一次，后续调用直接返回已有实例
    /// </summary>
    /// <param name="corpId">企业 ID</param>
    /// <param name="secret">会话存档 Secret</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>已初始化的 SDK 实例（调用方不得单独 Dispose）</returns>
    public async Task<WeComFinanceSdk> GetSdkAsync(string corpId, string secret, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var key = BuildKey(corpId, secret);

        if (_sdks.TryGetValue(key, out var existing))
            return existing;

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_sdks.TryGetValue(key, out existing))
                return existing;

            await Interop.FinanceSdkBootstrapper.GetOrResolveSdkDirectoryAsync(cancellationToken).ConfigureAwait(false);

            var sdk = new WeComFinanceSdk(corpId, secret);
            await sdk.InitializeAsync(cancellationToken).ConfigureAwait(false);
            _sdks[key] = sdk;
            return sdk;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// 健康检查：对一个已缓存的 SDK 实例做轻量探测（seq=0,limit=1）
    /// </summary>
    /// <param name="corpId">企业 ID</param>
    /// <param name="secret">会话存档 Secret</param>
    /// <returns>健康则返回 true</returns>
    public async Task<bool> HealthCheckAsync(string corpId, string secret)
    {
        var key = BuildKey(corpId, secret);
        if (!_sdks.TryGetValue(key, out var sdk))
            return false;

        try
        {
            var _ = await sdk.GetChatDataAsync(0, 1, timeout: 5).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 从缓存中移除并销毁指定企业的 SDK 实例
    /// </summary>
    public void Evict(string corpId, string secret)
    {
        var key = BuildKey(corpId, secret);
        if (_sdks.TryRemove(key, out var sdk))
            sdk.Dispose();
    }

    #region IDisposable / IAsyncDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var (_, sdk) in _sdks)
        {
            try { sdk.Dispose(); } catch { /* best-effort */ }
        }
        _sdks.Clear();
        _initLock.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var (_, sdk) in _sdks)
        {
            try { await sdk.DisposeAsync().ConfigureAwait(false); } catch { /* best-effort */ }
        }
        _sdks.Clear();
        _initLock.Dispose();
    }

    #endregion

    private static string BuildKey(string corpId, string secret)
    {
        // 简单哈希避免在 key 中暴露 secret 明文
        var hash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(secret)));
        return $"{corpId}:{hash[..16]}";
    }
}
