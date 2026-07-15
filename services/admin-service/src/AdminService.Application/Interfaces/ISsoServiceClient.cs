using AdminService.Application.Common;
using AdminService.Application.DTOs;

namespace AdminService.Application.Interfaces;

public record SsoUserInfo(ulong Id, string Username, string Email, string ReviewStatus);

/// <summary>
/// 集群内直连 sso-service 的 /internal/users/... 接口（Service DNS，不经网关），
/// 供审核编排流程查询用户信息、把用户标记为已审核/已拒绝，供开户向导查询待审核用户列表。
/// </summary>
public interface ISsoServiceClient
{
    Task<SsoUserInfo?> GetUserAsync(ulong userId, CancellationToken cancellationToken);
    Task ApproveReviewAsync(ulong userId, ulong reviewedBy, CancellationToken cancellationToken);

    /// <summary>
    /// 拒绝审核。sso-service 侧会软删除该用户，拒绝不可撤销。返回 false 表示用户不存在
    /// （sso-service 404，含"已被拒绝过"的情况——软删除后同样查不到），调用方据此判断。
    /// </summary>
    Task<bool> RejectReviewAsync(ulong userId, ulong reviewedBy, CancellationToken cancellationToken);

    /// <summary>
    /// 分页查询指定审核状态的用户，reviewStatus 为 null 时由 sso-service 侧默认取 pending。
    /// sortBy 的合法字段（id/createdAt）由 sso-service 自行校验，非法值 sso-service 返回 400，
    /// 本方法直接把 sso-service 的 400 以 ValidationException 形式向上抛出。
    /// </summary>
    Task<PagedResult<SsoUserInfo>> ListUsersAsync(
        string? reviewStatus, int page, int pageSize, string? sortBy, SortOrder sortOrder, CancellationToken cancellationToken);
}
