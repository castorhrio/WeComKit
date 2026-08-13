using System.Xml.Linq;
using WeComKit.Core;

namespace WeComKit.Tests;

/// <summary>
/// WeComCallbackXmlReader 安全解析测试。
/// </summary>
public class WeComCallbackXmlReaderTests
{
    [Fact]
    public void Parse_ExtractsTopLevelFields()
    {
        var xml = "<xml><ToUserName><![CDATA[corp]]></ToUserName><Encrypt><![CDATA[abc123]]></Encrypt></xml>";

        var root = WeComCallbackXmlReader.Parse(xml);

        Assert.Equal("corp", root.Element("ToUserName")?.Value);
        Assert.Equal("abc123", root.Element("Encrypt")?.Value);
    }

    [Fact]
    public void Parse_RejectsDtd()
    {
        var xml = """
                  <?xml version="1.0"?>
                  <!DOCTYPE foo [
                    <!ELEMENT foo ANY>
                  ]>
                  <foo/>
                  """;

        Assert.Throws<WeComCallbackXmlException>(() => WeComCallbackXmlReader.Parse(xml));
    }

    [Fact]
    public void Parse_RejectsXxeExternalEntity()
    {
        // 外部实体引用 → DtdProcessing.Prohibit 应阻止
        var xml = """
                  <?xml version="1.0"?>
                  <!DOCTYPE foo [
                    <!ENTITY xxe SYSTEM "file:///etc/passwd">
                  ]>
                  <foo>&xxe;</foo>
                  """;

        Assert.Throws<WeComCallbackXmlException>(() => WeComCallbackXmlReader.Parse(xml));
    }

    [Fact]
    public void Parse_RejectsMalformedXml()
    {
        Assert.Throws<WeComCallbackXmlException>(() => WeComCallbackXmlReader.Parse("<not-closed>"));
    }

    [Fact]
    public void Parse_RejectsOversizedPayload()
    {
        // 超过 MaxXmlCharacters 字符上限
        var huge = "<x>" + new string('a', WeComCallbackXmlReader.MaxXmlCharacters) + "</x>";

        Assert.Throws<WeComCallbackXmlException>(() => WeComCallbackXmlReader.Parse(huge));
    }

    [Fact]
    public void Parse_ReturnsXElement_NotFlatDictionary()
    {
        // 重复 / 嵌套节点应保留结构
        var xml = "<xml><Item>a</Item><Item>b</Item></xml>";

        var root = WeComCallbackXmlReader.Parse(xml);

        var items = root.Elements("Item").ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal("a", items[0].Value);
        Assert.Equal("b", items[1].Value);
    }

    [Fact]
    public void Parse_RejectsNullInput()
    {
        Assert.Throws<WeComCallbackXmlException>(() => WeComCallbackXmlReader.Parse(null!));
    }
}
