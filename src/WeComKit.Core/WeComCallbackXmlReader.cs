using System.Xml;
using System.Xml.Linq;

namespace WeComKit.Core;

/// <summary>
/// 回调 XML 安全解析器
///
/// 将外部输入视为不可信数据，禁止 DTD / 外部实体，并限制文档规模，防止 XXE 与超大 Payload 攻击。
/// 仅负责安全解析，返回 <see cref="XElement"/>，由调用方按需提取字段（避免发明可能丢信息的扁平化数据模型）。
/// </summary>
public static class WeComCallbackXmlReader
{
    /// <summary>最大 XML 字符数（1,048,576）</summary>
    public const int MaxXmlCharacters = 1_048_576;

    /// <summary>
    /// 安全解析回调 XML。
    /// </summary>
    /// <param name="xml">原始 XML 字符串</param>
    /// <returns>解析后的根 <see cref="XElement"/></returns>
    /// <exception cref="WeComCallbackXmlException">malformed / XXE / DTD / 超大 Payload</exception>
    public static XElement Parse(string xml)
    {
        if (xml is null)
            throw new WeComCallbackXmlException("XML 不能为空");

        // 入口预检（字符数）：避免把超大字符串无脑塞进 parser
        if (xml.Length > MaxXmlCharacters)
            throw new WeComCallbackXmlException($"XML 超过最大字符数限制 ({MaxXmlCharacters})");

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,   // 禁止 DTD → 同时阻断 XXE
            XmlResolver = null,                        // 禁止外部实体解析
            MaxCharactersInDocument = MaxXmlCharacters // parser 层级的大小上限
        };

        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), settings);
            return XElement.Load(reader);
        }
        catch (XmlException ex)
        {
            throw new WeComCallbackXmlException("XML 解析失败", ex);
        }
    }
}
