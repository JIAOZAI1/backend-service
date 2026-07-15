using AdminService.Application.Common;
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

    /// <summary>
    /// 拒绝审核。只调用 sso-service 一步，不涉及租户/数据库实例——被拒绝的用户不开户。
    /// sso-service 侧会把该用户软删除，拒绝不可撤销：拒绝后无法再对同一 userId 调用
    /// approve/reject（sso-service 侧查不到该用户，统一返回 404）。用户不存在同样返回 404
    /// （NotFoundException），与幂等无关的其他异常包装为 ReviewStepFailedException。
    /// </summary>
    Task RejectAsync(ulong userId, ulong reviewedBy, CancellationToken cancellationToken);

    /// <summary>
    /// 分页查询指定审核状态的用户，供开户向导展示待审核列表。reviewStatus 缺省时由
    /// sso-service 默认取 pending。sortBy 白名单由 sso-service 校验，非法值转换为
    /// ValidationException 向上抛出（与本服务其他分页接口的错误处理方式一致）。
    /// </summary>
    Task<PagedResult<SsoUserInfo>> ListUsersAsync(
        string? reviewStatus, int page, int pageSize, string? sortBy, SortOrder sortOrder, CancellationToken cancellationToken);
}
