using AdminService.Application.Common;
using AdminService.Application.Exceptions;
using AdminService.Application.Interfaces;
using AdminService.Application.Services;
using AdminService.Domain.Entities;
using Moq;
using Shouldly;

namespace AdminService.UnitTests;

public class ReviewServiceTests
{
    private readonly Mock<ISsoServiceClient> _ssoClient = new();
    private readonly Mock<IJobServiceClient> _jobClient = new();
    private readonly Mock<ITenantRepository> _tenantRepository = new();
    private readonly Mock<IUserTenantRepository> _userTenantRepository = new();

    private readonly TenantDatabaseOptions _dbOptions = new() { DbType = "mysql", Host = "192.168.8.184", Port = 3306 };

    private ReviewService CreateService() => new(
        _ssoClient.Object,
        _jobClient.Object,
        _tenantRepository.Object,
        _userTenantRepository.Object,
        _dbOptions);

    [Fact]
    public async Task ApproveAsync_NewUser_CreatesTenantThenProvisionsThenMarksReviewed()
    {
        const ulong userId = 42;
        const ulong reviewedBy = 1;
        var callOrder = new List<string>();

        _ssoClient.Setup(c => c.GetUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SsoUserInfo(userId, "alice", "alice@example.com", "pending"))
            .Callback(() => callOrder.Add("fetch-user"));

        _userTenantRepository.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserTenant?)null);

        _tenantRepository.Setup(r => r.AddAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("create-tenant"))
            .Returns(Task.CompletedTask);
        _tenantRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        _userTenantRepository.Setup(r => r.AddAsync(It.IsAny<UserTenant>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("link-user-tenant"))
            .Returns(Task.CompletedTask);
        _userTenantRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        _jobClient.Setup(c => c.CreateTenantProvisioningJobAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("provision-database"))
            .ReturnsAsync(123L);

        _ssoClient.Setup(c => c.ApproveReviewAsync(userId, reviewedBy, It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("mark-user-reviewed"))
            .Returns(Task.CompletedTask);

        var service = CreateService();
        var result = await service.ApproveAsync(userId, reviewedBy, CancellationToken.None);

        result.UserId.ShouldBe(userId);
        result.Tenant.DbName.ShouldStartWith("tenant_");
        result.Tenant.DbUsername.ShouldBe(result.Tenant.DbName);
        result.Tenant.Status.ShouldBe(TenantStatus.Active);

        callOrder.ShouldBe(["fetch-user", "create-tenant", "link-user-tenant", "provision-database", "mark-user-reviewed"]);

        _tenantRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ApproveAsync_UserAlreadyHasTenant_SkipsTenantCreationButStillProvisionsAndReviewsIdempotently()
    {
        const ulong userId = 42;
        const ulong reviewedBy = 1;

        var existingTenant = new Tenant
        {
            Id = 7,
            TenantId = Guid.NewGuid().ToString(),
            TenantCode = "abcd1234wxyz",
            DbType = "mysql",
            DbHost = "192.168.8.184",
            DbPort = 3306,
            DbName = "tenant_abcd1234wxyz",
            DbUsername = "tenant_abcd1234wxyz",
            DbPassword = "existing-password",
            ReviewedBy = reviewedBy,
            Status = TenantStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _ssoClient.Setup(c => c.GetUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SsoUserInfo(userId, "alice", "alice@example.com", "approved"));

        _userTenantRepository.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserTenant { Id = 1, UserId = userId, TenantId = existingTenant.Id, CreatedAt = DateTime.UtcNow });

        _tenantRepository.Setup(r => r.GetByIdAsync(existingTenant.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTenant);

        _jobClient.Setup(c => c.CreateTenantProvisioningJobAsync(
                existingTenant.DbName, existingTenant.DbUsername, existingTenant.DbPassword, It.IsAny<CancellationToken>()))
            .ReturnsAsync(123L);

        _ssoClient.Setup(c => c.ApproveReviewAsync(userId, reviewedBy, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();
        var result = await service.ApproveAsync(userId, reviewedBy, CancellationToken.None);

        result.Tenant.TenantCode.ShouldBe(existingTenant.TenantCode);
        _tenantRepository.Verify(r => r.AddAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()), Times.Never);
        _userTenantRepository.Verify(r => r.AddAsync(It.IsAny<UserTenant>(), It.IsAny<CancellationToken>()), Times.Never);
        // 已是 Active，不应再次调用 SaveChangesAsync 做状态流转
        _tenantRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApproveAsync_UserNotFound_ThrowsNotFoundException()
    {
        _ssoClient.Setup(c => c.GetUserAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SsoUserInfo?)null);

        var service = CreateService();

        await Should.ThrowAsync<NotFoundException>(() => service.ApproveAsync(1, 1, CancellationToken.None));
    }

    [Fact]
    public async Task ApproveAsync_JobServiceCallFails_ThrowsReviewStepFailedExceptionWithStepName()
    {
        const ulong userId = 42;

        _ssoClient.Setup(c => c.GetUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SsoUserInfo(userId, "alice", "alice@example.com", "pending"));
        _userTenantRepository.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserTenant?)null);
        _tenantRepository.Setup(r => r.AddAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _tenantRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _userTenantRepository.Setup(r => r.AddAsync(It.IsAny<UserTenant>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _userTenantRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        _jobClient.Setup(c => c.CreateTenantProvisioningJobAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection refused"));

        var service = CreateService();

        var ex = await Should.ThrowAsync<ReviewStepFailedException>(() => service.ApproveAsync(userId, 1, CancellationToken.None));
        ex.Step.ShouldBe("provision-database");

        _ssoClient.Verify(c => c.ApproveReviewAsync(It.IsAny<ulong>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
