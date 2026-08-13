using System.Reflection;
using System.Runtime.InteropServices;

namespace WeComKit.MsgAudit.Interop;

/// <summary>
/// 企业微信会话存档 SDK 原生函数声明
/// 对应官方 C++ SDK：libWeWorkFinanceSdk / WeWorkFinanceSdk.dll
///
/// DLL 加载链：NativeLibrary.SetDllImportResolver → FinanceSdkBootstrapper.GetOrResolveSdkDirectory()
/// </summary>
internal static class FinanceSdkNative
{
    private const string DllName = "WeWorkFinanceSdk";

    #region SDK 生命周期

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr NewSdk();

    /// <summary>初始化 SDK。句柄以 SafeHandle 形式传入，运行时会在调用期间增加引用计数。</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int Init(SdkHandle sdk, [MarshalAs(UnmanagedType.LPStr)] string corpid, [MarshalAs(UnmanagedType.LPStr)] string secret);

    /// <summary>
    /// 释放 SDK 句柄（原始 IntPtr 入口）。
    /// 仅供 <see cref="SdkHandle.ReleaseHandle"/> 调用，不应在普通业务路径中使用。
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "DestroySdk")]
    public static extern void DestroySdkRaw(IntPtr sdk);

    #endregion

    #region 消息操作

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetChatData(SdkHandle sdk, ulong seq, uint limit, [MarshalAs(UnmanagedType.LPStr)] string? proxy, [MarshalAs(UnmanagedType.LPStr)] string? passwd, int timeout, SliceHandle chatDatas);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int DecryptData([MarshalAs(UnmanagedType.LPStr)] string encrypt_key, [MarshalAs(UnmanagedType.LPStr)] string encrypt_msg, SliceHandle msg);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetMediaData(SdkHandle sdk, [MarshalAs(UnmanagedType.LPStr)] string indexbuf, [MarshalAs(UnmanagedType.LPStr)] string sdkFileid, [MarshalAs(UnmanagedType.LPStr)] string? proxy, [MarshalAs(UnmanagedType.LPStr)] string? passwd, int timeout, MediaDataHandle media_data);

    #endregion

    #region Slice（字符串数据读取）

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr NewSlice();

    /// <summary>释放 Slice（原始 IntPtr 入口），仅供 <see cref="SliceHandle.ReleaseHandle"/> 调用。</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "FreeSlice")]
    public static extern void FreeSliceRaw(IntPtr slice);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr GetContentFromSlice(SliceHandle slice);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetSliceLen(SliceHandle slice);

    #endregion

    #region MediaData（媒体文件分片下载）

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr NewMediaData();

    /// <summary>释放 MediaData（原始 IntPtr 入口），仅供 <see cref="MediaDataHandle.ReleaseHandle"/> 调用。</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "FreeMediaData")]
    public static extern void FreeMediaDataRaw(IntPtr mediaData);

    // 注意：GetData 返回的是 borrowed buffer 指针（非受托管句柄），故返回 IntPtr；
    // 调用方需确保 mediaData 在使用该指针期间保持存活（在 using 块内使用）。
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr GetData(MediaDataHandle mediaData);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetDataLen(MediaDataHandle mediaData);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int IsMediaDataFinish(MediaDataHandle mediaData);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr GetOutIndexBuf(MediaDataHandle mediaData);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetIndexLen(MediaDataHandle mediaData);

    #endregion

    #region 平台与 DLL 加载

    private static readonly string[] WindowsDeps = { "libcrypto-3-x64.dll", "libssl-3-x64.dll", "libcurl-x64.dll" };

    private static bool _resolverRegistered;
    private static readonly object _resolverLock = new();

    public static string PlatformSdkFileName => FinanceSdkBootstrapper.PlatformSdkFileName;

    public static string[] PlatformRequiredFiles => FinanceSdkBootstrapper.PlatformRequiredFiles;

    /// <summary>
    /// 注册 DLL 导入解析器，首次调用时通过 Bootstrapper 发现 SDK
    /// </summary>
    public static void EnsureResolverRegistered()
    {
        if (_resolverRegistered) return;

        lock (_resolverLock)
        {
            if (_resolverRegistered) return;

            var sdkDir = FinanceSdkBootstrapper.GetOrResolveSdkDirectory();

            // Windows: 预加载 OpenSSL + libcurl 依赖
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                foreach (var depDll in WindowsDeps)
                {
                    var depPath = Path.Combine(sdkDir, depDll);
                    if (File.Exists(depPath))
                        NativeLibrary.TryLoad(depPath, out _);
                }
            }

            NativeLibrary.SetDllImportResolver(
                typeof(FinanceSdkNative).Assembly,
                (name, assembly, searchPath) => ResolveDllCore(name));

            _resolverRegistered = true;
        }
    }

    /// <summary>
    /// 每次 P/Invoke 时触发，从 Bootstrapper 实时获取 SDK 目录
    /// 确保 SetSdkDirectory() 后无需重新注册 resolver
    /// </summary>
    private static IntPtr ResolveDllCore(string libraryName)
    {
        if (libraryName != DllName)
            return IntPtr.Zero;

        var sdkDir = FinanceSdkBootstrapper.GetOrResolveSdkDirectory();
        var dllPath = Path.Combine(sdkDir, PlatformSdkFileName);

        if (!NativeLibrary.TryLoad(dllPath, out var handle))
        {
            throw new DllNotFoundException(
                $"无法加载企业微信会话存档 SDK: {dllPath}。" +
                $"请确认 {PlatformSdkFileName} 及依赖库已放置到正确目录。");
        }

        return handle;
    }

    /// <summary>
    /// 检查 SDK 文件是否存在于当前 Bootstrapper 解析的目录
    /// </summary>
    public static (bool Available, List<string> MissingFiles) CheckFiles()
    {
        string sdkDir;
        try
        {
            sdkDir = FinanceSdkBootstrapper.GetOrResolveSdkDirectory();
        }
        catch (DllNotFoundException)
        {
            var allMissing = new List<string>(PlatformRequiredFiles);
            return (false, allMissing);
        }

        var missing = new List<string>();
        foreach (var file in PlatformRequiredFiles)
        {
            if (!File.Exists(Path.Combine(sdkDir, file)))
                missing.Add(file);
        }

        return (missing.Count == 0, missing);
    }

    #endregion
}
