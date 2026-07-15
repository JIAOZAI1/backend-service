using AdminService.Application.DTOs;

namespace AdminService.Application.Interfaces;

public interface IReviewService
{
    /// <summary>
    /// 审核通过并触发开户作业。同步部分只做校验用户、校验数据库实例、建租户记录、创建开户
    /// Job，创建成功后立即返回 jobId，不等待 Job 执行完成——"标记 sso-service 已审核"
    /// "租户置 Active"已挪到 Job 内的 Task 执行（见 JobServiceClient.CreateTenantProvisioningJobAsync）。
    /// 任一同步步骤失败抛出 ReviewStepFailedException，不做自动回滚；已落库/已触发的步骤保持
    /// 原样，管理员可用同一个 userId 重试（各步骤均幂等）。
    /// </summary>
    Task<ApproveReviewResponse> ApproveAsync(
        ulong userId, long databaseInstanceId, ulong reviewedBy, CancellationToken cancellationToken);
}
