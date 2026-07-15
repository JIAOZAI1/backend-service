using AdminService.Domain.Entities;

namespace AdminService.Application.DTOs;

public class TenantResponse
{
    public long Id { get; init; }
    public required string TenantId { get; init; }
    public required string TenantCode { get; init; }
    public required string DbType { get; init; }
    public required string DbHost { get; init; }
    public int DbPort { get; init; }
    public required string DbName { get; init; }
    public required string DbUsername { get; init; }
    public ulong ReviewedBy { get; init; }
    public TenantStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }

    public static TenantResponse FromEntity(Tenant tenant) => new()
    {
        Id = tenant.Id,
        TenantId = tenant.TenantId,
        TenantCode = tenant.TenantCode,
        DbType = tenant.DbType,
        DbHost = tenant.DbHost,
        DbPort = tenant.DbPort,
        DbName = tenant.DbName,
        DbUsername = tenant.DbUsername,
        ReviewedBy = tenant.ReviewedBy,
        Status = tenant.Status,
        CreatedAt = tenant.CreatedAt,
    };
}

public record ApproveReviewRequest(long DatabaseInstanceId);

public class ApproveReviewResponse
{
    public required ulong UserId { get; init; }
    public required TenantResponse Tenant { get; init; }

    /// <summary>开户作业 ID，前端凭此轮询 GET /backend-job-service/api/v1/jobs/{jobId}/status。</summary>
    public required long JobId { get; init; }
}
