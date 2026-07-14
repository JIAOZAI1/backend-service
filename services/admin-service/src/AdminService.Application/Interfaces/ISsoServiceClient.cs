namespace AdminService.Application.Interfaces;

public record SsoUserInfo(ulong Id, string Username, string Email, string ReviewStatus);

/// <summary>
/// 集群内直连 sso-service 的 /internal/users/... 接口（Service DNS，不经网关），
/// 供审核编排流程查询用户信息、把用户标记为已审核。
/// </summary>
public interface ISsoServiceClient
{
    Task<SsoUserInfo?> GetUserAsync(ulong userId, CancellationToken cancellationToken);
    Task ApproveReviewAsync(ulong userId, ulong reviewedBy, CancellationToken cancellationToken);
}
