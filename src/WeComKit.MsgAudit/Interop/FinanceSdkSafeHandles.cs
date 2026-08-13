using System.Runtime.InteropServices;

namespace WeComKit.MsgAudit.Interop;

/// <summary>
/// 企业微信会话存档 SDK 句柄（Sdk*）的安全包装。
///
/// 设计要点：
/// - 普通 P/Invoke（Init / GetChatData / GetMediaData ...）直接接收本类型，
///   .NET 运行时会在 native 调用期间自动增加引用计数，避免调用过程中句柄被关闭。
/// - 释放函数 <see cref="FinanceSdkNative.DestroySdkRaw"/> 接收原始 <see cref="IntPtr"/>，
///   仅由 <see cref="ReleaseHandle"/> 调用；释放路径不应再把 SafeHandle 交给 marshaller。
/// - 不使用 <c>DangerousGetHandle</c> 读取受托管句柄。
/// </summary>
internal sealed class SdkHandle : SafeHandle
{
    public SdkHandle() : base(IntPtr.Zero, ownsHandle: true) { }

    public override bool IsInvalid => handle == IntPtr.Zero;

    /// <summary>
    /// 创建并接管 NewSdk() 返回的原始指针。
    /// </summary>
    public static SdkHandle Create()
    {
        var sdk = new SdkHandle();
        sdk.SetHandle(FinanceSdkNative.NewSdk());
        return sdk;
    }

    /// <summary>
    /// 接管一个已由调用方获取（并可能已做错误处理 / 日志）的原始 NewSdk() 指针。
    /// 不会为 null 指针抛出（由调用方在 wrap 前自行检查）。
    /// </summary>
    internal static SdkHandle FromRaw(IntPtr raw)
    {
        var sdk = new SdkHandle();
        sdk.SetHandle(raw);
        return sdk;
    }

    protected override bool ReleaseHandle()
    {
        // 释放路径直接用原始 IntPtr，不经过 SafeHandle marshaller
        try { FinanceSdkNative.DestroySdkRaw(handle); }
        catch { /* best-effort：ReleaseHandle 不应让异常逃逸 */ }
        return true;
    }
}

/// <summary>
/// Slice（字符串数据缓冲）的安全包装。
/// </summary>
internal sealed class SliceHandle : SafeHandle
{
    public SliceHandle() : base(IntPtr.Zero, ownsHandle: true) { }

    public override bool IsInvalid => handle == IntPtr.Zero;

    public static SliceHandle Create()
    {
        var slice = new SliceHandle();
        slice.SetHandle(FinanceSdkNative.NewSlice());
        return slice;
    }

    protected override bool ReleaseHandle()
    {
        try { FinanceSdkNative.FreeSliceRaw(handle); }
        catch { /* best-effort */ }
        return true;
    }
}

/// <summary>
/// MediaData（媒体分片缓冲）的安全包装。
/// </summary>
internal sealed class MediaDataHandle : SafeHandle
{
    public MediaDataHandle() : base(IntPtr.Zero, ownsHandle: true) { }

    public override bool IsInvalid => handle == IntPtr.Zero;

    public static MediaDataHandle Create()
    {
        var media = new MediaDataHandle();
        media.SetHandle(FinanceSdkNative.NewMediaData());
        return media;
    }

    protected override bool ReleaseHandle()
    {
        try { FinanceSdkNative.FreeMediaDataRaw(handle); }
        catch { /* best-effort */ }
        return true;
    }
}
