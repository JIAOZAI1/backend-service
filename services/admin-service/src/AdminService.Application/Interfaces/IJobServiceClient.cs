namespace AdminService.Application.Interfaces;

/// <summary>
/// 集群内直连 backend-job-service 的作业 API（Service DNS，不经网关），供审核编排流程
/// 触发开户作业。四个任务均幂等，重复创建同名作业不影响下游插件执行结果的正确性。
/// </summary>
public interface IJobServiceClient
{
    /// <summary>
    /// 创建一次性开户作业，按顺序挂载四个任务：
    /// 1. mysql-create-database（建库）
    /// 2. mysql-create-user（建用户并授权，dbPassword 是新建租户数据库用户的密码，
    ///    与 databaseInstanceId 对应实例的管理员凭据是两个不同的密钥）
    /// 3. sso-mark-user-reviewed（调 sso-service 标记用户已审核）
    /// 4. admin-activate-tenant（调 admin-service 自身的内部接口把租户置 Active）
    /// 前一个任务失败则后续任务不执行，因此"建用户失败"不会误标记为"已审核"。返回作业 ID。
    /// </summary>
    Task<long> CreateTenantProvisioningJobAsync(
        long databaseInstanceId,
        string dbName,
        string dbUsername,
        string dbPassword,
        ulong userId,
        ulong reviewedBy,
        string tenantId,
        CancellationToken cancellationToken);
}
