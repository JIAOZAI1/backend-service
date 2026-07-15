namespace AdminService.Application.Interfaces;

/// <summary>
/// 供集群内其他服务（backend-job-service 的 admin-activate-tenant 插件）调用的租户状态回写用例，
/// 与只读的 ITenantQueryService 分开，避免把"仅内部调用"的写操作混进对外查询接口的契约里。
/// </summary>
public interface ITenantInternalService
{
    /// <summary>把租户置为 Active。幂等：已是 Active 时直接返回，不重复写库。</summary>
    Task ActivateAsync(string tenantId, CancellationToken cancellationToken);
}
