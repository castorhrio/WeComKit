using System.Text.Json.Serialization;

namespace WeComKit.Api.Models;

/// <summary>
/// 成员基本信息（简略列表）。
/// </summary>
public class UserSimpleInfo
{
    [JsonPropertyName("userid")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("department")]
    public long[] Department { get; set; } = Array.Empty<long>();

    [JsonPropertyName("open_userid")]
    public string OpenUserId { get; set; } = string.Empty;
}

/// <summary>
/// 成员详细信息（同时作为 /user/get 的响应基类，含 errcode/errmsg）。
/// </summary>
public class UserDetailInfo : WeComApiResult
{
    [JsonPropertyName("userid")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("department")]
    public long[] Department { get; set; } = Array.Empty<long>();

    [JsonPropertyName("order")]
    public uint[] Order { get; set; } = Array.Empty<uint>();

    [JsonPropertyName("position")]
    public string Position { get; set; } = string.Empty;

    [JsonPropertyName("mobile")]
    public string Mobile { get; set; } = string.Empty;

    [JsonPropertyName("gender")]
    public string Gender { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("biz_mail")]
    public string BizMail { get; set; } = string.Empty;

    [JsonPropertyName("is_leader_in_dept")]
    public int[] IsLeaderInDept { get; set; } = Array.Empty<int>();

    [JsonPropertyName("direct_leader")]
    public string[] DirectLeader { get; set; } = Array.Empty<string>();

    [JsonPropertyName("avatar")]
    public string Avatar { get; set; } = string.Empty;

    [JsonPropertyName("thumb_avatar")]
    public string ThumbAvatar { get; set; } = string.Empty;

    [JsonPropertyName("telephone")]
    public string Telephone { get; set; } = string.Empty;

    [JsonPropertyName("alias")]
    public string Alias { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("enable")]
    public int Enable { get; set; }

    [JsonPropertyName("qr_code")]
    public string QrCode { get; set; } = string.Empty;

    [JsonPropertyName("external_profile")]
    public UserExternalProfile? ExternalProfile { get; set; }

    [JsonPropertyName("external_position")]
    public string ExternalPosition { get; set; } = string.Empty;

    [JsonPropertyName("address")]
    public string Address { get; set; } = string.Empty;

    [JsonPropertyName("open_userid")]
    public string OpenUserId { get; set; } = string.Empty;

    [JsonPropertyName("main_department")]
    public long MainDepartment { get; set; }
}

/// <summary>
/// 成员外部属性。
/// </summary>
public class UserExternalProfile
{
    [JsonPropertyName("external_corp_name")]
    public string ExternalCorpName { get; set; } = string.Empty;

    [JsonPropertyName("wechat_channels")]
    public WechatChannelsInfo? WechatChannels { get; set; }

    [JsonPropertyName("external_attr")]
    public List<ExternalAttr> ExternalAttr { get; set; } = new();
}

/// <summary>
/// 视频号信息。
/// </summary>
public class WechatChannelsInfo
{
    [JsonPropertyName("nickname")]
    public string NickName { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public int Status { get; set; }
}

/// <summary>
/// 外部属性。
/// </summary>
public class ExternalAttr
{
    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public ExternalAttrText? Text { get; set; }

    [JsonPropertyName("web")]
    public ExternalAttrWeb? Web { get; set; }

    [JsonPropertyName("miniprogram")]
    public ExternalAttrMiniprogram? Miniprogram { get; set; }
}

public class ExternalAttrText
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

public class ExternalAttrWeb
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
}

public class ExternalAttrMiniprogram
{
    [JsonPropertyName("appid")]
    public string AppId { get; set; } = string.Empty;

    [JsonPropertyName("pagepath")]
    public string PagePath { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
}

/// <summary>
/// 成员列表响应。
/// </summary>
public class UserListResponse<T> : WeComApiResult
{
    [JsonPropertyName("userlist")]
    public List<T> UserList { get; set; } = new();
}

/// <summary>
/// 创建/更新成员请求。
/// </summary>
public class UserRequest
{
    [JsonPropertyName("userid")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("department")]
    public long[]? Department { get; set; }

    [JsonPropertyName("mobile")]
    public string? Mobile { get; set; }

    [JsonPropertyName("position")]
    public string? Position { get; set; }

    [JsonPropertyName("gender")]
    public string? Gender { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("telephone")]
    public string? Telephone { get; set; }

    [JsonPropertyName("enable")]
    public int? Enable { get; set; }

    [JsonPropertyName("avatar_mediaid")]
    public string? AvatarMediaId { get; set; }

    [JsonPropertyName("main_department")]
    public long? MainDepartment { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("alias")]
    public string? Alias { get; set; }
}
