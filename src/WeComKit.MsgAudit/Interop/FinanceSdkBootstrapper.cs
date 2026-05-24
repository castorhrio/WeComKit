using System.Runtime.InteropServices;

namespace WeComKit.MsgAudit.Interop;

/// <summary>
/// 企业微信会话存档 SDK 原生文件定位器
///
/// SDK 发现优先级（从高到低）：
/// 1. 进程内调用 SetSdkDirectory() 显式设置
/// 2. 环境变量 WECOM_MSG_AUDIT_SDK_DIR 指定的目录
/// 3. %LocalAppData%/WeComKit/msg-audit-sdk/ 本地缓存
/// 4. AppContext.BaseDirectory（兼容手动放置）
/// </summary>
internal static class FinanceSdkBootstrapper
{
    private static readonly string[] WindowsDeps = { "libcrypto-3-x64.dll", "libssl-3-x64.dll", "libcurl-x64.dll" };
    private static readonly string[] LinuxDeps = Array.Empty<string>();

    private static string? _sdkDirectory;
    private static readonly object _lock = new();

    private const string CacheDirName = "WeComKit";
    private const string SdkSubDir = "msg-audit-sdk";
    private const string EnvSdkDir = "WECOM_MSG_AUDIT_SDK_DIR";

    #region 公开 API

    /// <summary>
    /// 获取已验证的 SDK 目录（同步，不含自动下载）
    /// 供 DLL 导入解析器和构造函数使用
    /// </summary>
    public static string GetOrResolveSdkDirectory()
    {
        if (_sdkDirectory is not null)
            return _sdkDirectory;

        lock (_lock)
        {
            if (_sdkDirectory is not null)
                return _sdkDirectory;

            _sdkDirectory = ResolveSdkDirectory() ?? ThrowNotFound();
        }

        return _sdkDirectory;
    }

    /// <summary>
    /// 获取已验证的 SDK 目录。
    /// </summary>
    public static Task<string> GetOrResolveSdkDirectoryAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(GetOrResolveSdkDirectory());
    }

    /// <summary>
    /// 显式设置 SDK 目录（覆盖所有自动发现逻辑）
    /// </summary>
    public static void SetSdkDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentNullException(nameof(directory));
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException($"SDK 目录不存在: {directory}");
        if (!ValidateDirectory(directory))
            throw new FileNotFoundException($"SDK 目录缺少当前平台所需主文件 {PlatformSdkFileName}: {directory}");

        lock (_lock)
        {
            _sdkDirectory = directory;
        }
    }

    /// <summary>
    /// 检查平台所需文件（用于诊断输出）
    /// </summary>
    public static string[] PlatformRequiredFiles =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[] { "WeWorkFinanceSdk.dll", "libcrypto-3-x64.dll", "libssl-3-x64.dll", "libcurl-x64.dll" }
            : new[] { "libWeWorkFinanceSdk.so" };

    /// <summary>
    /// 主 SDK 文件名
    /// </summary>
    public static string PlatformSdkFileName =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "WeWorkFinanceSdk.dll"
        : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "libWeWorkFinanceSdk.so"
        : throw new PlatformNotSupportedException($"不支持的操作系统: {RuntimeInformation.OSDescription}");

    /// <summary>
    /// 获取本地缓存目录（不保证已安装）
    /// </summary>
    public static string CacheDirectory => GetCacheDirectory();

    #endregion

    #region 内部实现

    /// <summary>
    /// 同步解析 SDK 目录（不触发网络 I/O，避免在 DLL 解析器回调中阻塞）
    /// </summary>
    private static string? ResolveSdkDirectory()
    {
        // 1. 环境变量
        var envDir = Environment.GetEnvironmentVariable(EnvSdkDir);
        if (!string.IsNullOrWhiteSpace(envDir) && ValidateDirectory(envDir))
            return envDir;

        // 2. 本地缓存
        var cacheDir = GetCacheDirectory();
        if (ValidateDirectory(cacheDir))
            return cacheDir;

        // 3. AppContext.BaseDirectory 兼容
        var baseDir = AppContext.BaseDirectory;
        if (ValidateDirectory(baseDir))
            return baseDir;

        return null;
    }

    private static bool ValidateDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return false;

        // SDK 主文件存在即可
        var sdkFile = Path.Combine(directory, PlatformSdkFileName);
        return File.Exists(sdkFile);
    }

    private static string GetCacheDirectory()
    {
        var root = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (string.IsNullOrWhiteSpace(root))
            root = Path.GetTempPath();

        return Path.Combine(root, CacheDirName, SdkSubDir);
    }

    private static string ThrowNotFound()
        => throw new DllNotFoundException(BuildNotFoundMessage());

    private static string BuildNotFoundMessage()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("未找到企业微信会话存档 SDK 原生文件。");
        sb.AppendLine();
        sb.AppendLine("获取方式（任选一种）：");
        sb.AppendLine();
        sb.AppendLine("  1. 登录企业微信管理后台 → 管理工具 → 会话内容存档 → 下载 SDK");
        sb.AppendLine("     将解压后的文件放到应用输出目录（bin/ 下）");
        sb.AppendLine();
        sb.AppendLine($"  2. 设置环境变量 {EnvSdkDir} 指向 SDK 所在目录");
        sb.AppendLine($"     例如: set {EnvSdkDir}=C:\\wecom-sdk\\");
        sb.AppendLine();
        sb.Append("当前平台所需文件: ");
        sb.AppendLine(PlatformSdkFileName);
        foreach (var dep in (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? WindowsDeps : LinuxDeps))
            sb.AppendLine($"  - {dep}");

        return sb.ToString();
    }

    #endregion
}
