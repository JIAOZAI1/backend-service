namespace AdminService.Application.Interfaces;

/// <summary>
/// 集群内直连 backend-job-service 的作业 API（Service DNS，不经网关），供审核编排流程
/// 触发"建库 + 建用户"作业。两个内置插件（mysql-create-database/mysql-create-user）本身幂等，
/// 重复创建同名作业不影响下游插件执行结果的正确性。
/// </summary>
public interface IJobServiceClient
{
    /// <summary>创建一次性作业并按顺序挂载"创建数据库"“创建用户并授权"两个任务，返回作业 ID。</summary>
    Task<long> CreateTenantProvisioningJobAsync(
        string dbName,
        string dbUsername,
        string dbPassword,
        CancellationToken cancellationToken);
}
