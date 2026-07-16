using AdminService.Application.Common;
using AdminService.Domain.Entities;

namespace AdminService.Application.Interfaces;

/// <summary>
/// 跨服务查询/写入 sso-service 拥有的 users/roles/user_roles 表（两服务共用同一个 MySQL
/// 数据库，见 AdminDbContext 上的 SsoUser/SsoRole/SsoUserRole DbSet），供系统管理员的
/// 用户管理页面（含租户信息）与密码重置功能使用，不通过 HTTP 调用 sso-service。
/// </summary>
public interface IUserManagementRepository
{
    /// <summary>
    /// 分页查询用户，JOIN 角色（roles/user_roles）与当前 active 租户（tenants/user_tenants），
    /// 不限 reviewStatus——供管理员做日常全量用户管理，区别于审核流程专用的
    /// ITenantQueryService/ReviewController 相关查询。
    /// </summary>
    Task<(List<UserWithTenantRow> Items, long Total)> ListUsersWithTenantAsync(
        int page, int pageSize, SortSpec sort, CancellationToken cancellationToken);

    Task<SsoUser?> GetUserByIdAsync(ulong userId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>ListUsersWithTenantAsync 单行查询结果——角色列表已聚合好，租户信息可能为空。</summary>
public record UserWithTenantRow(
    ulong Id,
    string Username,
    string Email,
    IReadOnlyList<string> Roles,
    bool Enabled,
    string? TenantCode,
    DateTime? LicenseExpiresAt,
    DateTime CreatedAt);
