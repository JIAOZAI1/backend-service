using AdminService.Application.Common;
using AdminService.Application.DTOs;

namespace AdminService.Application.Interfaces;

public interface IUserManagementService
{
    /// <summary>
    /// 分页查询全量用户（不限 reviewStatus），含角色与当前 active 租户信息，
    /// 供系统管理员做日常用户管理，区别于开户审核流程专用的待审核列表。
    /// </summary>
    Task<PagedResult<UserWithTenantResponse>> ListUsersAsync(
        int page, int pageSize, string? sortBy, SortOrder sortOrder, CancellationToken cancellationToken);

    /// <summary>
    /// 重置用户密码：生成随机临时密码，bcrypt hash 后直接写 users.password_hash
    /// （与 sso-service 的 bcrypt.DefaultCost 兼容，bcrypt 密文自带 cost 参数，
    /// 跨实现验证不受影响）。新密码只在这一次响应里明文返回，不落库、不记录日志。
    /// </summary>
    Task<ResetPasswordResponse> ResetPasswordAsync(ulong userId, CancellationToken cancellationToken);

    /// <summary>按 ID 查询任意用户详情（不限 reviewStatus/租户状态），供管理员用户管理页面使用。</summary>
    Task<UserDetailResponse> GetUserAsync(ulong userId, CancellationToken cancellationToken);

    /// <summary>
    /// 启用/禁用用户：直接写 users.status（与 sso-service model.UserStatusActive/Disabled
    /// 取值一致），登录校验在 sso-service auth_service 里，禁用后该用户下次登录会被拒绝。
    /// 操作幂等，对已处于目标状态的用户重复调用不报错。
    /// </summary>
    Task SetUserEnabledAsync(ulong userId, bool enabled, CancellationToken cancellationToken);
}
