using System.Text.Json.Serialization;

namespace WeComKit.Api.Models;

#region 部门

/// <summary>
/// 部门信息（用于列表/查询响应）
/// </summary>
public class DepartmentInfo
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("name_en")]
    public string NameEn { get; set; } = string.Empty;

    [JsonPropertyName("parentid")]
    public long ParentId { get; set; }

    [JsonPropertyName("order")]
    public uint Order { get; set; }
}

/// <summary>
/// 部门列表响应
/// </summary>
public class DepartmentListResponse : WeComApiResult
{
    [JsonPropertyName("department")]
    public List<DepartmentInfo> Department { get; set; } = new();
}

/// <summary>
/// 创建部门响应（返回部门 ID）
/// </summary>
public class DepartmentCreateResponse : WeComApiResult
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
}

/// <summary>
/// 创建/更新部门请求
/// </summary>
public class DepartmentRequest
{
    /// <summary>部门名称（必需，最大 32 字符，同一层级不可重名）</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>英文名称</summary>
    [JsonPropertyName("name_en")]
    public string? NameEn { get; set; }

    /// <summary>父部门 ID（根部门为 1）</summary>
    [JsonPropertyName("parentid")]
    public long ParentId { get; set; }

    /// <summary>在父部门中的排序值（越大越靠前）</summary>
    [JsonPropertyName("order")]
    public uint? Order { get; set; }

    /// <summary>部门 ID（更新时必须指定）</summary>
    [JsonPropertyName("id")]
    public long? Id { get; set; }
}

#endregion
