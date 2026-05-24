using WeComKit.Api.Http;
using WeComKit.Api.Models;

namespace WeComKit.Api;

/// <summary>
/// 企业微信通讯录管理 API
/// 部门管理 + 成员管理
/// </summary>
public class WeComContactsApi
{
    private readonly WeComHttpClient _client;

    public WeComContactsApi(WeComHttpClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    #region 部门管理

    /// <summary>
    /// 创建部门
    /// </summary>
    public Task<DepartmentCreateResponse> CreateDepartmentAsync(DepartmentRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("部门名称不能为空", nameof(request));

        return _client.PostAsync<DepartmentCreateResponse>("/cgi-bin/department/create", request, ct);
    }

    /// <summary>
    /// 更新部门
    /// </summary>
    public Task<WeComApiResult> UpdateDepartmentAsync(DepartmentRequest request, CancellationToken ct = default)
    {
        if (request.Id is null || request.Id <= 0)
            throw new ArgumentException("更新部门时 Id 不能为空", nameof(request));

        return _client.PostAsync<WeComApiResult>("/cgi-bin/department/update", request, ct);
    }

    /// <summary>
    /// 删除部门
    /// </summary>
    /// <param name="departmentId">部门 ID（不可删除根部门及包含子部门/成员的部门）</param>
    public Task<WeComApiResult> DeleteDepartmentAsync(long departmentId, CancellationToken ct = default)
    {
        return _client.GetAsync<WeComApiResult>("/cgi-bin/department/delete", new Dictionary<string, string>
        {
            ["id"] = departmentId.ToString()
        }, ct);
    }

    /// <summary>
    /// 获取部门列表（包含所有字段）
    /// </summary>
    /// <param name="parentId">父部门 ID。填 null 获取全量组织架构</param>
    public async Task<List<DepartmentInfo>> GetDepartmentListAsync(long? parentId = null, CancellationToken ct = default)
    {
        var query = new Dictionary<string, string>();
        if (parentId.HasValue)
            query["id"] = parentId.Value.ToString();

        var result = await _client.GetAsync<DepartmentListResponse>("/cgi-bin/department/list", query, ct);
        return result.Department;
    }

    /// <summary>
    /// 获取部门简略列表（仅 id / name / parentid / order）
    /// </summary>
    public async Task<List<DepartmentInfo>> GetDepartmentSimpleListAsync(long? parentId = null, CancellationToken ct = default)
    {
        var query = new Dictionary<string, string>();
        if (parentId.HasValue)
            query["id"] = parentId.Value.ToString();

        var result = await _client.GetAsync<DepartmentListResponse>("/cgi-bin/department/simplelist", query, ct);
        return result.Department;
    }

    #endregion

    #region 成员管理

    /// <summary>
    /// 创建成员
    /// </summary>
    public Task<WeComApiResult> CreateUserAsync(UserRequest request, CancellationToken ct = default)
    {
        ValidateUserRequest(request, isCreate: true);
        return _client.PostAsync<WeComApiResult>("/cgi-bin/user/create", request, ct);
    }

    /// <summary>
    /// 更新成员
    /// </summary>
    public Task<WeComApiResult> UpdateUserAsync(UserRequest request, CancellationToken ct = default)
    {
        ValidateUserRequest(request, isCreate: false);
        return _client.PostAsync<WeComApiResult>("/cgi-bin/user/update", request, ct);
    }

    /// <summary>
    /// 删除成员
    /// </summary>
    public Task<WeComApiResult> DeleteUserAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId 不能为空", nameof(userId));

        return _client.GetAsync<WeComApiResult>("/cgi-bin/user/delete", new Dictionary<string, string>
        {
            ["userid"] = userId
        }, ct);
    }

    /// <summary>
    /// 获取成员详细信息
    /// </summary>
    public Task<UserDetailInfo> GetUserAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId 不能为空", nameof(userId));

        return _client.GetAsync<UserDetailInfo>("/cgi-bin/user/get", new Dictionary<string, string>
        {
            ["userid"] = userId
        }, ct);
    }

    /// <summary>
    /// 获取部门成员简略列表
    /// </summary>
    /// <param name="departmentId">部门 ID</param>
    /// <param name="fetchChild">是否递归获取子部门成员。默认 0</param>
    public async Task<List<UserSimpleInfo>> GetDepartmentUserSimpleListAsync(long departmentId, int fetchChild = 0, CancellationToken ct = default)
    {
        var result = await _client.GetAsync<UserListResponse<UserSimpleInfo>>("/cgi-bin/user/simplelist", new Dictionary<string, string>
        {
            ["department_id"] = departmentId.ToString(),
            ["fetch_child"] = fetchChild.ToString()
        }, ct);
        return result.UserList;
    }

    /// <summary>
    /// 获取部门成员详细列表
    /// </summary>
    /// <param name="departmentId">部门 ID</param>
    /// <param name="fetchChild">是否递归获取子部门成员。默认 0</param>
    public async Task<List<UserDetailInfo>> GetDepartmentUserListAsync(long departmentId, int fetchChild = 0, CancellationToken ct = default)
    {
        var result = await _client.GetAsync<UserListResponse<UserDetailInfo>>("/cgi-bin/user/list", new Dictionary<string, string>
        {
            ["department_id"] = departmentId.ToString(),
            ["fetch_child"] = fetchChild.ToString()
        }, ct);
        return result.UserList;
    }

    #endregion

    #region 校验

    private static void ValidateUserRequest(UserRequest request, bool isCreate)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
            throw new ArgumentException("UserId 不能为空");
        if (isCreate && string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("成员名称不能为空");
        if (isCreate && (request.Department is null || request.Department.Length == 0))
            throw new ArgumentException("成员部门不能为空");
        if (isCreate && string.IsNullOrWhiteSpace(request.Mobile) && string.IsNullOrWhiteSpace(request.Email))
            throw new ArgumentException("手机号和邮箱至少填写一个");
    }

    #endregion
}
