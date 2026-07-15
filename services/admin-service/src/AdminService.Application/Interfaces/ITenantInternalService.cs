namespace AdminService.Application.Interfaces;

/// <summary>
/// 供集群内其他服务（backend-job-service 的 admin-activate-tenant/admin-expire-overdue-tenants
/// 插件）调用的租户状态回写用例，与只读的 ITenantQueryService 分开，避免把"仅内部调用"的写操作
/// 混进对外查询接口的契约里。
/// </summary>
public interface ITenantInternalService
{
    /// <summary>把租户置为 Active。幂等：已是 Active 时直接返回，不重复写库。</summary>
    Task ActivateAsync(string tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// 批量检查所有 Status=Active 且 License 已过期（LicenseExpiresAt 早于当前时间）的租户，
    /// 置为 Expired。幂等：已经是 Expired 的租户不在查询范围内，不会重复处理。
    /// 供每日 License 监控 Job 调用，返回本次实际流转的租户数量。
    /// </summary>
    Task<int> ExpireOverdueTenantsAsync(CancellationToken cancellationToken);
}
