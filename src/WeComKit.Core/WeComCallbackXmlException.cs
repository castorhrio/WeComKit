namespace WeComKit.Core;

/// <summary>
/// 回调 XML 解析异常。
/// 用于 <see cref="WeComCallbackXmlReader"/> 在遇到 malformed / XXE / DTD / 超大 Payload 时的统一异常。
/// </summary>
public sealed class WeComCallbackXmlException : Exception
{
    public WeComCallbackXmlException(string message) : base(message) { }
    public WeComCallbackXmlException(string message, Exception innerException) : base(message, innerException) { }
}
