using AdminService.Domain.Entities;

namespace AdminService.Application.Interfaces;

public interface IUserTenantRepository
{
    /// <summary>已 Include 关联的 Tenant 导航属性缺失，只返回 user_tenants 行本身；
    /// 需要租户详情时用 UserId 反查 ITenantRepository。</summary>
    Task<UserTenant?> GetByUserIdAsync(ulong userId, CancellationToken cancellationToken);
    Task AddAsync(UserTenant userTenant, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
