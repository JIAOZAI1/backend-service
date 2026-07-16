namespace AdminService.Application.DTOs;

/// <summary>
/// 管理员用户列表的单条记录：sso-service 拥有的用户基本信息/角色，
/// 与 admin-service 拥有的租户信息（tenant_code、License 到期时间）合并在一起。
/// 用户未开户或所属租户非 active 状态时，TenantCode/LicenseExpiresAt 为空。
/// </summary>
public class UserWithTenantResponse
{
    public ulong Id { get; init; }
    public required string Username { get; init; }
    public required string Email { get; init; }
    public required IReadOnlyList<string> Roles { get; init; }
    public bool Enabled { get; init; }
    public string? TenantCode { get; init; }
    public DateTime? LicenseExpiresAt { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>密码重置结果：新密码只在这一次响应里以明文返回，不落库、不记录日志，管理员需当场转告用户。</summary>
public record ResetPasswordResponse(string NewPassword);

/// <summary>按 ID 查询单个用户的详情，不含租户信息（列表接口 UserWithTenantResponse 才带租户）。</summary>
public class UserDetailResponse
{
    public ulong Id { get; init; }
    public required string Username { get; init; }
    public required string Email { get; init; }
    public bool Enabled { get; init; }
    public DateTime CreatedAt { get; init; }
}
