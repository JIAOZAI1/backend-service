using AdminService.Application.Common;
using AdminService.Domain.Entities;

namespace AdminService.Application.Interfaces;

public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(long id, CancellationToken cancellationToken);

    /// <summary>按业务键 TenantId（GUID 字符串）查找，供跨服务调用方使用——它们只应引用这个
    /// 对外暴露的业务键，不应依赖内部自增主键 Id（见 tenants.tenant_id 唯一索引）。</summary>
    Task<Tenant?> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken);

    Task<(List<Tenant> Items, long Total)> ListPagedAsync(int page, int pageSize, SortSpec sort, CancellationToken cancellationToken);

    /// <summary>查询所有 Status=Active 且 LicenseExpiresAt 早于 asOf 的租户，供每日 License
    /// 监控 Job 批量流转为 Expired。Created/Cancelled 状态的租户不参与——未激活的租户没有
    /// "过期"的意义，已取消的也不该被改回 Expired。</summary>
    Task<List<Tenant>> ListOverdueActiveTenantsAsync(DateTime asOf, CancellationToken cancellationToken);

    Task AddAsync(Tenant tenant, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
