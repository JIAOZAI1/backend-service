using AdminService.Application.Common;
using AdminService.Application.DTOs;
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
    private readonly Mock<IDatabaseInstanceRepository> _databaseInstanceRepository = new();

    private static readonly DatabaseInstance DefaultDatabaseInstance = new()
    {
        Id = 9,
        Name = "primary-mysql",
        DbType = DatabaseInstanceType.MySql,
        Host = "192.168.8.184",
        Port = 3306,
        Username = "admin",
        EncryptedPassword = "cipher-text",
    };

    private ReviewService CreateService() => new(
        _ssoClient.Object,
        _jobClient.Object,
        _tenantRepository.Object,
        _userTenantRepository.Object,
        _databaseInstanceRepository.Object);

    [Fact]
    public async Task ApproveAsync_NewUser_CreatesTenantThenProvisionsAndReturnsJobId()
    {
        const ulong userId = 42;
        const ulong reviewedBy = 1;
        var callOrder = new List<string>();

        _ssoClient.Setup(c => c.GetUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SsoUserInfo(userId, "alice", "alice@example.com", "pending"))
            .Callback(() => callOrder.Add("fetch-user"));

        _databaseInstanceRepository.Setup(r => r.GetByIdAsync(DefaultDatabaseInstance.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultDatabaseInstance)
            .Callback(() => callOrder.Add("validate-database-instance"));

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
                DefaultDatabaseInstance.Id,
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                userId, reviewedBy, It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("provision-database"))
            .ReturnsAsync(123L);

        var service = CreateService();
        var result = await service.ApproveAsync(userId, DefaultDatabaseInstance.Id, reviewedBy, CancellationToken.None);

        result.UserId.ShouldBe(userId);
        result.JobId.ShouldBe(123L);
        result.Tenant.DbName.ShouldStartWith("tenant_");
        result.Tenant.DbUsername.ShouldBe(result.Tenant.DbName);
        result.Tenant.DbHost.ShouldBe(DefaultDatabaseInstance.Host);
        result.Tenant.DbPort.ShouldBe(DefaultDatabaseInstance.Port);
        // 同步部分不再置 Active——留给 Job 内的 admin-activate-tenant 任务完成
        result.Tenant.Status.ShouldBe(TenantStatus.Created);

        callOrder.ShouldBe(["fetch-user", "validate-database-instance", "create-tenant", "link-user-tenant", "provision-database"]);

        // 同步部分只在建租户记录时写一次库，不再有"置 Active"这次额外的 SaveChangesAsync
        _tenantRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _ssoClient.Verify(c => c.ApproveReviewAsync(It.IsAny<ulong>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApproveAsync_UserAlreadyHasTenant_SkipsTenantCreationButStillProvisionsIdempotently()
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
            DatabaseInstanceId = DefaultDatabaseInstance.Id,
            ReviewedBy = reviewedBy,
            Status = TenantStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _ssoClient.Setup(c => c.GetUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SsoUserInfo(userId, "alice", "alice@example.com", "approved"));

        _databaseInstanceRepository.Setup(r => r.GetByIdAsync(DefaultDatabaseInstance.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultDatabaseInstance);

        _userTenantRepository.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserTenant { Id = 1, UserId = userId, TenantId = existingTenant.Id, CreatedAt = DateTime.UtcNow });

        _tenantRepository.Setup(r => r.GetByIdAsync(existingTenant.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTenant);

        _jobClient.Setup(c => c.CreateTenantProvisioningJobAsync(
                DefaultDatabaseInstance.Id,
                existingTenant.DbName, existingTenant.DbUsername, existingTenant.DbPassword,
                userId, reviewedBy, existingTenant.TenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(456L);

        var service = CreateService();
        var result = await service.ApproveAsync(userId, DefaultDatabaseInstance.Id, reviewedBy, CancellationToken.None);

        result.JobId.ShouldBe(456L);
        result.Tenant.TenantCode.ShouldBe(existingTenant.TenantCode);
        _tenantRepository.Verify(r => r.AddAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()), Times.Never);
        _userTenantRepository.Verify(r => r.AddAsync(It.IsAny<UserTenant>(), It.IsAny<CancellationToken>()), Times.Never);
        _tenantRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApproveAsync_UserNotFound_ThrowsNotFoundException()
    {
        _ssoClient.Setup(c => c.GetUserAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SsoUserInfo?)null);

        var service = CreateService();

        await Should.ThrowAsync<NotFoundException>(() => service.ApproveAsync(1, DefaultDatabaseInstance.Id, 1, CancellationToken.None));
    }

    [Fact]
    public async Task ApproveAsync_DatabaseInstanceNotFound_ThrowsNotFoundExceptionWithoutCreatingJob()
    {
        const ulong userId = 42;

        _ssoClient.Setup(c => c.GetUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SsoUserInfo(userId, "alice", "alice@example.com", "pending"));
        _databaseInstanceRepository.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DatabaseInstance?)null);

        var service = CreateService();

        await Should.ThrowAsync<NotFoundException>(() => service.ApproveAsync(userId, 999, 1, CancellationToken.None));

        _jobClient.Verify(c => c.CreateTenantProvisioningJobAsync(
            It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<ulong>(), It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApproveAsync_JobServiceCallFails_ThrowsReviewStepFailedExceptionWithStepName()
    {
        const ulong userId = 42;

        _ssoClient.Setup(c => c.GetUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SsoUserInfo(userId, "alice", "alice@example.com", "pending"));
        _databaseInstanceRepository.Setup(r => r.GetByIdAsync(DefaultDatabaseInstance.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultDatabaseInstance);
        _userTenantRepository.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserTenant?)null);
        _tenantRepository.Setup(r => r.AddAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _tenantRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _userTenantRepository.Setup(r => r.AddAsync(It.IsAny<UserTenant>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _userTenantRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        _jobClient.Setup(c => c.CreateTenantProvisioningJobAsync(
                It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<ulong>(), It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection refused"));

        var service = CreateService();

        var ex = await Should.ThrowAsync<ReviewStepFailedException>(
            () => service.ApproveAsync(userId, DefaultDatabaseInstance.Id, 1, CancellationToken.None));
        ex.Step.ShouldBe("provision-database");
    }

    [Fact]
    public async Task RejectAsync_UserExists_CallsSsoServiceReject()
    {
        const ulong userId = 42;
        const ulong reviewedBy = 1;

        _ssoClient.Setup(c => c.RejectReviewAsync(userId, reviewedBy, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService();
        await service.RejectAsync(userId, reviewedBy, CancellationToken.None);

        _ssoClient.Verify(c => c.RejectReviewAsync(userId, reviewedBy, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RejectAsync_UserNotFound_ThrowsNotFoundException()
    {
        _ssoClient.Setup(c => c.RejectReviewAsync(It.IsAny<ulong>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = CreateService();

        await Should.ThrowAsync<NotFoundException>(() => service.RejectAsync(999, 1, CancellationToken.None));
    }

    [Fact]
    public async Task RejectAsync_SsoServiceCallFails_ThrowsReviewStepFailedExceptionWithStepName()
    {
        _ssoClient.Setup(c => c.RejectReviewAsync(It.IsAny<ulong>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection refused"));

        var service = CreateService();

        var ex = await Should.ThrowAsync<ReviewStepFailedException>(() => service.RejectAsync(42, 1, CancellationToken.None));
        ex.Step.ShouldBe("reject-user");
    }

    [Fact]
    public async Task ListUsersAsync_DelegatesToSsoServiceClient()
    {
        var expected = new PagedResult<SsoUserInfo>
        {
            Items = [new SsoUserInfo(1, "alice", "alice@example.com", "pending")],
            Page = 1,
            PageSize = 20,
            Total = 1,
        };
        _ssoClient.Setup(c => c.ListUsersAsync("pending", 1, 20, "createdAt", SortOrder.Asc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var service = CreateService();
        var result = await service.ListUsersAsync("pending", 1, 20, "createdAt", SortOrder.Asc, CancellationToken.None);

        result.Total.ShouldBe(expected.Total);
        result.Items.ShouldHaveSingleItem();
        result.Items[0].Username.ShouldBe("alice");
    }
}
