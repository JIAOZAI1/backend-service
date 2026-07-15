using AdminService.Application.Common;
using AdminService.Application.DTOs;
using AdminService.Application.Exceptions;
using AdminService.Application.Interfaces;
using AdminService.Domain.Entities;

namespace AdminService.Application.Services;

public class ReviewService(
    ISsoServiceClient ssoServiceClient,
    IJobServiceClient jobServiceClient,
    ITenantRepository tenantRepository,
    IUserTenantRepository userTenantRepository,
    IDatabaseInstanceRepository databaseInstanceRepository) : IReviewService
{
    public async Task<ApproveReviewResponse> ApproveAsync(
        ulong userId, long databaseInstanceId, ulong reviewedBy, CancellationToken cancellationToken)
    {
        var ssoUser = await CallStep("fetch-user", () => ssoServiceClient.GetUserAsync(userId, cancellationToken))
            ?? throw new NotFoundException($"user {userId} not found");

        var databaseInstance = await CallStep("validate-database-instance",
                () => databaseInstanceRepository.GetByIdAsync(databaseInstanceId, cancellationToken))
            ?? throw new NotFoundException($"database instance {databaseInstanceId} not found");

        var userTenant = await CallStep("load-existing-tenant", () => userTenantRepository.GetByUserIdAsync(userId, cancellationToken));

        Tenant tenant;
        if (userTenant is not null)
        {
            // 已有租户记录：说明此前至少完成过一次开户，走幂等短路，不重新生成密码/租户 code。
            tenant = await CallStep("load-existing-tenant", () => tenantRepository.GetByIdAsync(userTenant.TenantId, cancellationToken))
                ?? throw new ReviewStepFailedException("load-existing-tenant", $"user_tenants 引用的 tenant {userTenant.TenantId} 不存在");
        }
        else
        {
            tenant = await CallStep("create-tenant", () => CreateTenantAsync(databaseInstance, reviewedBy, cancellationToken));

            await CallStep("link-user-tenant", () => LinkUserTenantAsync(userId, tenant.Id, cancellationToken));
        }

        var jobId = await CallStep("provision-database", () => jobServiceClient.CreateTenantProvisioningJobAsync(
            databaseInstanceId: databaseInstance.Id,
            dbName: tenant.DbName,
            dbUsername: tenant.DbUsername,
            dbPassword: tenant.DbPassword,
            userId: userId,
            reviewedBy: reviewedBy,
            tenantId: tenant.TenantId,
            cancellationToken));

        return new ApproveReviewResponse { UserId = userId, Tenant = TenantResponse.FromEntity(tenant), JobId = jobId };
    }

    public async Task RejectAsync(ulong userId, ulong reviewedBy, CancellationToken cancellationToken)
    {
        var rejected = await CallStep("reject-user", () => ssoServiceClient.RejectReviewAsync(userId, reviewedBy, cancellationToken));
        if (!rejected)
        {
            throw new NotFoundException($"user {userId} not found");
        }
    }

    public Task<PagedResult<SsoUserInfo>> ListUsersAsync(
        string? reviewStatus, int page, int pageSize, string? sortBy, SortOrder sortOrder, CancellationToken cancellationToken) =>
        ssoServiceClient.ListUsersAsync(reviewStatus, page, pageSize, sortBy, sortOrder, cancellationToken);

    private async Task<Tenant> CreateTenantAsync(DatabaseInstance databaseInstance, ulong reviewedBy, CancellationToken cancellationToken)
    {
        var tenantCode = TenantCodeGenerator.Generate();
        var now = DateTime.UtcNow;
        var tenant = new Tenant
        {
            TenantId = Guid.NewGuid().ToString(),
            TenantCode = tenantCode,
            DbType = databaseInstance.DbType.ToString().ToLowerInvariant(),
            DbHost = databaseInstance.Host,
            DbPort = databaseInstance.Port,
            DbName = $"tenant_{tenantCode}",
            DbUsername = $"tenant_{tenantCode}",
            DbPassword = SecurePasswordGenerator.Generate(),
            DatabaseInstanceId = databaseInstance.Id,
            ReviewedBy = reviewedBy,
            Status = TenantStatus.Created,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await tenantRepository.AddAsync(tenant, cancellationToken);
        await tenantRepository.SaveChangesAsync(cancellationToken);
        return tenant;
    }

    private async Task LinkUserTenantAsync(ulong userId, long tenantId, CancellationToken cancellationToken)
    {
        var userTenant = new UserTenant
        {
            UserId = userId,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow,
        };
        await userTenantRepository.AddAsync(userTenant, cancellationToken);
        await userTenantRepository.SaveChangesAsync(cancellationToken);
    }

    private static async Task<T> CallStep<T>(string step, Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (ex is not ReviewStepFailedException and not NotFoundException)
        {
            throw new ReviewStepFailedException(step, ex.Message);
        }
    }

    private static async Task CallStep(string step, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex) when (ex is not ReviewStepFailedException and not NotFoundException)
        {
            throw new ReviewStepFailedException(step, ex.Message);
        }
    }
}
