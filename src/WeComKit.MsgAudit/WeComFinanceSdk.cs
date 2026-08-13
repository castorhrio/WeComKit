using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using WeComKit.Core;
using WeComKit.MsgAudit.Interop;
using WeComKit.MsgAudit.Models;

namespace WeComKit.MsgAudit;

/// <summary>
/// 企业微信会话存档 SDK 托管封装
///
/// 线程安全：所有实例方法通过内部 SemaphoreSlim(1,1) 串行化（原生 Sdk* 指针不支持并发）。
///
/// 使用方式：
/// <code>
///   await using var sdk = await WeComFinanceSdk.CreateAndInitAsync(corpId, secret);
///   var response = await sdk.GetChatDataResponseAsync(seq: 0, limit: 1000);
///
///   foreach (var item in response.ChatData)
///   {
///       var key = WeComUtility.DecryptRsa(item.EncryptRandomKey, privateKeyPem);
///       var record = await sdk.DecryptChatRecordAsync(key, item.EncryptChatMsg);
///   }
/// </code>
/// </summary>
public class WeComFinanceSdk : IDisposable, IAsyncDisposable, IMsgAuditChatDataSource
{
    private SdkHandle? _sdkHandle;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile bool _disposed;
    private readonly string _corpId;
    private readonly string _secret;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>
    /// 初始化 SDK 封装实例（延迟初始化，首次调用时自动 Init）
    /// </summary>
    /// <param name="corpId">企业 ID</param>
    /// <param name="secret">会话存档 Secret</param>
    public WeComFinanceSdk(string corpId, string secret)
    {
        _corpId = corpId ?? throw new ArgumentNullException(nameof(corpId));
        _secret = secret ?? throw new ArgumentNullException(nameof(secret));
        FinanceSdkNative.EnsureResolverRegistered();
    }

    #region 静态工厂

    /// <summary>
    /// 创建并立即初始化 SDK 实例
    /// </summary>
    public static async Task<WeComFinanceSdk> CreateAndInitAsync(string corpId, string secret, CancellationToken ct = default)
    {
        // 先尝试解析，确保原生 SDK 可用
        await FinanceSdkBootstrapper.GetOrResolveSdkDirectoryAsync(ct).ConfigureAwait(false);

        var sdk = new WeComFinanceSdk(corpId, secret);
        await sdk.InitializeAsync(ct).ConfigureAwait(false);
        return sdk;
    }

    #endregion

    #region 同步 API

    /// <summary>
    /// 拉取会话消息，返回原始 JSON 字符串
    /// </summary>
    public string GetChatData(ulong seq, uint limit = 1000, int timeout = 30)
    {
        _gate.Wait();
        try
        {
            EnsureInitialized();
            return GetChatDataCore(seq, limit, timeout);
        }
        finally { _gate.Release(); }
    }

    /// <summary>
    /// 拉取会话消息并反序列化为 ChatDataResponse
    /// </summary>
    public ChatDataResponse GetChatDataResponse(ulong seq, uint limit = 1000, int timeout = 30)
    {
        var json = GetChatData(seq, limit, timeout);
        return JsonSerializer.Deserialize<ChatDataResponse>(json) ?? new ChatDataResponse();
    }

    /// <summary>
    /// 使用 SDK 解密单条消息内容为 ChatRecord
    /// </summary>
    public ChatRecord DecryptChatRecord(string decryptedKey, string encryptChatMsg)
    {
        var json = DecryptMessage(decryptedKey, encryptChatMsg);
        return JsonSerializer.Deserialize<ChatRecord>(json, JsonOptions)
               ?? throw new InvalidOperationException("消息反序列化失败");
    }

    /// <summary>
    /// 下载媒体文件到指定路径，自动处理分片
    /// </summary>
    public void DownloadFile(string sdkFileId, string savePath, int timeout = 30)
    {
        if (string.IsNullOrWhiteSpace(sdkFileId))
            throw new ArgumentNullException(nameof(sdkFileId));
        if (string.IsNullOrWhiteSpace(savePath))
            throw new ArgumentNullException(nameof(savePath));

        _gate.Wait();
        try
        {
            EnsureInitialized();

            var directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920);
            DownloadMediaCore(sdkFileId, fileStream, timeout);
        }
        finally { _gate.Release(); }
    }

    #endregion

    #region 异步 API

    /// <summary>
    /// 拉取会话消息（异步），返回原始 JSON 字符串
    /// </summary>
    public async Task<string> GetChatDataAsync(ulong seq, uint limit = 1000, int timeout = 30, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await EnsureInitializedAsync(ct).ConfigureAwait(false);
            return await Task.Run(() => GetChatDataCore(seq, limit, timeout), ct).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    /// <summary>
    /// 拉取会话消息并反序列化为 ChatDataResponse（异步）
    /// </summary>
    public async Task<ChatDataResponse> GetChatDataResponseAsync(ulong seq, uint limit = 1000, int timeout = 30, CancellationToken ct = default)
    {
        var json = await GetChatDataAsync(seq, limit, timeout, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<ChatDataResponse>(json) ?? new ChatDataResponse();
    }

    /// <summary>
    /// 解密单条消息为 ChatRecord（异步）
    /// </summary>
    public async Task<ChatRecord> DecryptChatRecordAsync(string decryptedKey, string encryptChatMsg, CancellationToken ct = default)
    {
        return await Task.Run(() => DecryptChatRecord(decryptedKey, encryptChatMsg), ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 下载媒体文件到指定路径（异步）
    /// </summary>
    public async Task DownloadFileAsync(string sdkFileId, string savePath, int timeout = 30, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sdkFileId))
            throw new ArgumentNullException(nameof(sdkFileId));
        if (string.IsNullOrWhiteSpace(savePath))
            throw new ArgumentNullException(nameof(savePath));

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await EnsureInitializedAsync(ct).ConfigureAwait(false);

            var directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920);
            await Task.Run(() => DownloadMediaCore(sdkFileId, fileStream, timeout), ct).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    /// <summary>
    /// 下载媒体文件并返回 MemoryStream
    /// 小文件适用；大文件（&gt;100MB）请用 <see cref="DownloadFileAsync"/> 直接写磁盘
    /// </summary>
    public async Task<MemoryStream> GetMediaStreamAsync(string sdkFileId, int timeout = 30, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sdkFileId))
            throw new ArgumentNullException(nameof(sdkFileId));

        var ms = new MemoryStream();
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await EnsureInitializedAsync(ct).ConfigureAwait(false);
            await Task.Run(() => DownloadMediaCore(sdkFileId, ms, timeout), ct).ConfigureAwait(false);
        }
        catch
        {
            // 下载异常时释放未完成的 Stream，避免泄漏
            await ms.DisposeAsync().ConfigureAwait(false);
            throw;
        }
        finally { _gate.Release(); }

        ms.Position = 0;
        return ms;
    }

    #endregion

    #region IMsgAuditChatDataSource（显式实现，供 MsgAuditSessionReader 使用）

    Task<ChatDataResponse> IMsgAuditChatDataSource.GetChatDataAsync(ulong seq, uint limit, CancellationToken cancellationToken)
        => GetChatDataResponseAsync(seq, limit, timeout: 30, cancellationToken);

    Task<ChatRecord> IMsgAuditChatDataSource.DecryptChatRecordAsync(string decryptedKey, string encryptChatMsg, CancellationToken cancellationToken)
        => DecryptChatRecordAsync(decryptedKey, encryptChatMsg, cancellationToken);

    #endregion

    #region 静态方法

    /// <summary>
    /// 解密消息内容（DecryptData 不依赖 Sdk*，天然线程安全）
    /// </summary>
    public static string DecryptMessage(string decryptedKey, string encryptMsg)
    {
        if (string.IsNullOrWhiteSpace(decryptedKey))
            throw new ArgumentNullException(nameof(decryptedKey));
        if (string.IsNullOrWhiteSpace(encryptMsg))
            throw new ArgumentNullException(nameof(encryptMsg));

        FinanceSdkNative.EnsureResolverRegistered();

        using var slice = SliceHandle.Create();
        try
        {
            ThrowOnError(FinanceSdkNative.DecryptData(decryptedKey, encryptMsg, slice), "DecryptData");
            return ReadSlice(slice);
        }
        catch (Exception ex) when (ex is AccessViolationException or SEHException)
        {
            throw new WeComFinanceSdkException(-1, "DecryptData", ex);
        }
    }

    /// <summary>
    /// 解密消息内容（异步）
    /// </summary>
    public static Task<string> DecryptMessageAsync(string decryptedKey, string encryptMsg, CancellationToken ct = default)
    {
        return Task.Run(() => DecryptMessage(decryptedKey, encryptMsg), ct);
    }

    /// <summary>
    /// 检查 SDK 文件是否存在
    /// </summary>
    public static (bool Available, List<string> MissingFiles) CheckAvailability()
    {
        return FinanceSdkNative.CheckFiles();
    }

    /// <summary>
    /// 仅诊断原生 SDK 文件发现和动态库加载，不调用 Init，也不会访问企业微信服务。
    /// </summary>
    public static List<string> DiagnoseNativeLoad()
    {
        var steps = new List<string>();
        var handles = new List<IntPtr>();

        try
        {
            var sdkDir = FinanceSdkBootstrapper.GetOrResolveSdkDirectory();
            steps.Add($"SDK 目录: {sdkDir}");

            var availability = FinanceSdkNative.CheckFiles();
            if (!availability.Available)
            {
                steps.Add("缺少文件:");
                foreach (var file in availability.MissingFiles)
                    steps.Add($"  - {file}");
                return steps;
            }

            foreach (var file in FinanceSdkNative.PlatformRequiredFiles)
            {
                var path = Path.Combine(sdkDir, file);
                try
                {
                    var handle = NativeLibrary.Load(path);
                    handles.Add(handle);
                    steps.Add($"加载 {file}: OK");
                }
                catch (Exception ex)
                {
                    steps.Add($"加载 {file}: FAIL - {ex.GetType().Name}: {ex.Message}");
                    return steps;
                }
            }

            steps.Add("原生 SDK 加载检查通过。");
            return steps;
        }
        catch (Exception ex)
        {
            steps.Add($"[FATAL] {ex.GetType().Name}: {ex.Message}");
            return steps;
        }
        finally
        {
            for (var i = handles.Count - 1; i >= 0; i--)
            {
                if (handles[i] != IntPtr.Zero)
                    NativeLibrary.Free(handles[i]);
            }
        }
    }

    /// <summary>
    /// 显式设置企业微信会话存档 SDK 原生文件目录。
    /// </summary>
    public static void SetSdkDirectory(string directory)
    {
        FinanceSdkBootstrapper.SetSdkDirectory(directory);
    }

    /// <summary>
    /// SDK 全链路诊断（文件 → 版本 → 加载 → Init → GetChatData → Destroy）
    /// </summary>
    public static List<string> Diagnose(string corpId, string secret)
    {
        var steps = new List<string>();
        string sdkDir;
        try
        {
            sdkDir = Interop.FinanceSdkBootstrapper.GetOrResolveSdkDirectory();
        }
        catch (DllNotFoundException ex)
        {
            steps.Add($"[FATAL] {ex.Message}");
            return steps;
        }

        FinanceSdkNative.EnsureResolverRegistered();

        // 1. SDK 目录与文件检查
        steps.Add("[1] SDK 目录与文件检查");
        steps.Add($"  SDK 目录: {sdkDir}");
        steps.Add($"  缓存目录: {Interop.FinanceSdkBootstrapper.CacheDirectory}");

        foreach (var file in FinanceSdkNative.PlatformRequiredFiles)
        {
            var fullPath = Path.Combine(sdkDir, file);
            if (File.Exists(fullPath))
            {
                var fi = new FileInfo(fullPath);
                steps.Add($"  {file}: OK ({fi.Length} bytes)");

                if (file == FinanceSdkNative.PlatformSdkFileName)
                {
                    try
                    {
                        var fvi = FileVersionInfo.GetVersionInfo(fullPath);
                        if (!string.IsNullOrEmpty(fvi.FileVersion))
                            steps.Add($"  SDK 文件版本: {fvi.FileVersion}");
                    }
                    catch { /* 非关键 */ }
                }
            }
            else
            {
                steps.Add($"  {file}: MISSING");
            }
        }

        steps.Add($"  操作系统: {RuntimeInformation.OSDescription}");
        steps.Add($"  运行时: {RuntimeInformation.FrameworkDescription}");

        // 2. DLL 加载
        steps.Add("[2] DLL 加载");
        var nativeHandles = new List<IntPtr>();
        try
        {
            var dllPath = Path.Combine(sdkDir, FinanceSdkNative.PlatformSdkFileName);

            // Windows: 预加载依赖
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                foreach (var dep in new[] { "libcrypto-3-x64.dll", "libssl-3-x64.dll", "libcurl-x64.dll" })
                {
                    var depPath = Path.Combine(sdkDir, dep);
                    if (File.Exists(depPath))
                    {
                        var ok = NativeLibrary.TryLoad(depPath, out var h);
                        if (ok && h != IntPtr.Zero)
                            nativeHandles.Add(h);
                        steps.Add($"  依赖 {dep}: {(ok && h != IntPtr.Zero ? "OK" : "FAIL")}");
                    }
                    else
                    {
                        steps.Add($"  依赖 {dep}: MISSING");
                    }
                }
            }

            var loaded = NativeLibrary.TryLoad(dllPath, out var handle);
            if (loaded && handle != IntPtr.Zero)
                nativeHandles.Add(handle);
            steps.Add($"  加载 {FinanceSdkNative.PlatformSdkFileName}: {(loaded && handle != IntPtr.Zero ? "OK" : "FAIL")}");
        }
        catch (Exception ex)
        {
            steps.Add($"  DLL 加载异常: {ex.GetType().Name} - {ex.Message}");
            return steps;
        }
        finally
        {
            for (var i = nativeHandles.Count - 1; i >= 0; i--)
            {
                if (nativeHandles[i] != IntPtr.Zero)
                    NativeLibrary.Free(nativeHandles[i]);
            }
        }

        // 3. SDK 功能测试
        steps.Add("[3] SDK 功能测试");
        SdkHandle? sdkHandle = null;
        SliceHandle? sliceHandle = null;
        try
        {
            var rawSdk = FinanceSdkNative.NewSdk();
            steps.Add($"  NewSdk: {(rawSdk != IntPtr.Zero ? "OK" : "NULL")}");
            if (rawSdk == IntPtr.Zero) return steps;

            sdkHandle = SdkHandle.FromRaw(rawSdk);

            var ret = FinanceSdkNative.Init(sdkHandle, corpId, secret);
            steps.Add($"  Init: {ret} {(ret == 0 ? "OK" : $"({((SdkErrorCode)ret).GetDescription()})")}");
            if (ret != 0) return steps;

            sliceHandle = SliceHandle.Create();
            ret = FinanceSdkNative.GetChatData(sdkHandle, 0, 1, null, null, 30, sliceHandle);
            var preview = ReadSlice(sliceHandle);
            steps.Add($"  GetChatData(seq=0,limit=1): {ret}, 长度={preview.Length}");

            sdkHandle.Dispose();
            sdkHandle = null;
            steps.Add("  DestroySdk: OK");
            steps.Add("诊断完成：全部通过");
        }
        catch (Exception ex)
        {
            steps.Add($"  SDK 测试异常: {ex.GetType().Name} - {ex.Message}");
        }
        finally
        {
            sliceHandle?.Dispose();
            sdkHandle?.Dispose();
        }

        return steps;
    }

    #endregion

    #region Slice 读取

    private static string ReadSlice(SliceHandle slice)
    {
        if (slice.IsInvalid) return string.Empty;
        var contentPtr = FinanceSdkNative.GetContentFromSlice(slice);
        var len = FinanceSdkNative.GetSliceLen(slice);
        if (contentPtr == IntPtr.Zero || len <= 0) return string.Empty;
        return Marshal.PtrToStringUTF8(contentPtr, len) ?? string.Empty;
    }

    private static void ThrowOnError(int retCode, string operation)
    {
        if (retCode != 0)
            throw new WeComFinanceSdkException(retCode, operation);
    }

    #endregion

    #region SDK 生命周期

    /// <summary>显式初始化 SDK（也可省略，首次调用时自动初始化）</summary>
    public void Initialize()
    {
        _gate.Wait();
        try { InitializeCore(); }
        finally { _gate.Release(); }
    }

    /// <summary>显式初始化 SDK（异步）</summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try { await Task.Run(InitializeCore, ct).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    private void InitializeCore()
    {
        if (_sdkHandle is not null) return;

        IntPtr raw;
        try { raw = FinanceSdkNative.NewSdk(); }
        catch (Exception ex) when (ex is AccessViolationException or SEHException or DllNotFoundException)
        { throw new WeComFinanceSdkException(-1, "NewSdk", ex); }

        if (raw == IntPtr.Zero)
            throw new WeComFinanceSdkException(-1, "NewSdk");

        var handle = SdkHandle.FromRaw(raw);

        try { ThrowOnError(FinanceSdkNative.Init(handle, _corpId, _secret), "Init"); }
        catch
        {
            handle.Dispose();
            throw;
        }

        _sdkHandle = handle;
    }

    private void EnsureInitialized()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_sdkHandle is null) InitializeCore();
    }

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_sdkHandle is null)
            await Task.Run(InitializeCore, ct).ConfigureAwait(false);
    }

    #endregion

    #region 核心私有方法（调用方已持锁）

    private string GetChatDataCore(ulong seq, uint limit, int timeout)
    {
        using var slice = SliceHandle.Create();
        try
        {
            ThrowOnError(FinanceSdkNative.GetChatData(_sdkHandle!, seq, limit, null, null, timeout, slice), "GetChatData");
            return ReadSlice(slice);
        }
        catch (Exception ex) when (ex is AccessViolationException or SEHException)
        {
            throw new WeComFinanceSdkException(-1, "GetChatData", ex);
        }
    }

    private void DownloadMediaCore(string sdkFileId, Stream target, int timeout)
    {
        string indexBuf = "";
        bool finished = false;

        while (!finished)
        {
            using var media = MediaDataHandle.Create();
            try
            {
                ThrowOnError(FinanceSdkNative.GetMediaData(_sdkHandle!, indexBuf, sdkFileId, null, null, timeout, media), "GetMediaData");

                var dataPtr = FinanceSdkNative.GetData(media);
                var dataLen = FinanceSdkNative.GetDataLen(media);

                // native length 不可信：负数 / 超大值直接拒绝，避免 Marshal.Copy 越界
                const int MaxChunkBytes = 256 * 1024 * 1024; // 256 MB
                if (dataLen > 0 && dataPtr != IntPtr.Zero && dataLen <= MaxChunkBytes)
                {
                    var buffer = ArrayPool<byte>.Shared.Rent(dataLen);
                    try
                    {
                        Marshal.Copy(dataPtr, buffer, 0, dataLen);
                        target.Write(buffer, 0, dataLen);
                    }
                    finally { ArrayPool<byte>.Shared.Return(buffer); }
                }

                finished = FinanceSdkNative.IsMediaDataFinish(media) == 1;
                if (!finished)
                {
                    var outIndexPtr = FinanceSdkNative.GetOutIndexBuf(media);
                    var outIndexLen = FinanceSdkNative.GetIndexLen(media);
                    // native 长度不可信：index buffer 正常很短，加一个上限避免恶意/异常返回触发超大分配。
                    const int MaxIndexBufBytes = 1024;
                    indexBuf = (outIndexPtr != IntPtr.Zero && outIndexLen > 0 && outIndexLen <= MaxIndexBufBytes)
                        ? Marshal.PtrToStringUTF8(outIndexPtr, outIndexLen) ?? ""
                        : "";
                }
            }
            catch (Exception ex) when (ex is AccessViolationException or SEHException)
            {
                throw new WeComFinanceSdkException(-1, "GetMediaData", ex);
            }
        }
    }

    #endregion

    #region IDisposable / IAsyncDisposable

    public void Dispose()
    {
        if (_disposed) return;

        _gate.Wait();
        try
        {
            if (_disposed) return;
            DisposeNative();
            _disposed = true;
        }
        finally { _gate.Release(); }

        _gate.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            DisposeNative();
            _disposed = true;
        }
        finally { _gate.Release(); }

        _gate.Dispose();
        GC.SuppressFinalize(this);
    }

    private void DisposeNative()
    {
        // SafeHandle.Dispose 负责实际释放；这里只需解除引用，让 GC 回收 SafeHandle。
        // 若已显式 Dispose，SafeHandle.ReleaseHandle 会通过 DestroySdkRaw 释放原生句柄。
        if (_sdkHandle is not null)
        {
            _sdkHandle.Dispose();
            _sdkHandle = null;
        }
    }

    ~WeComFinanceSdk()
    {
        DisposeNative();
    }

    #endregion
}
