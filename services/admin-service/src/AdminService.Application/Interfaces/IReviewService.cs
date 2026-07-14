using AdminService.Application.DTOs;

namespace AdminService.Application.Interfaces;

public interface IReviewService
{
    /// <summary>
    /// 审核通过并开户。任一步失败抛出 ReviewStepFailedException，不做自动回滚；
    /// 已落库/已触发的步骤保持原样，管理员可用同一个 userId 重试（各步骤均幂等）。
    /// </summary>
    Task<ApproveReviewResponse> ApproveAsync(ulong userId, ulong reviewedBy, CancellationToken cancellationToken);
}
