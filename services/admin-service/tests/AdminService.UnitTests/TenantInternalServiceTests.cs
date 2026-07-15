using AdminService.Application.Exceptions;
using AdminService.Application.Services;
using AdminService.Application.Interfaces;
using AdminService.Domain.Entities;
using Moq;
using Shouldly;

namespace AdminService.UnitTests;

public class TenantInternalServiceTests
{
    private readonly Mock<ITenantRepository> _tenantRepository = new();

    private TenantInternalService CreateService() => new(_tenantRepository.Object);

    private static Tenant NewTenant(TenantStatus status) => new()
    {
        Id = 7,
        TenantId = "d290f1ee-6c54-4b01-90e6-d701748f0851",
        TenantCode = "abcd1234wxyz",
        DbType = "mysql",
        DbHost = "192.168.8.184",
        DbPort = 3306,
        DbName = "tenant_abcd1234wxyz",
        DbUsername = "tenant_abcd1234wxyz",
        DbPassword = "pw",
        ReviewedBy = 1,
        Status = status,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    [Fact]
    public async Task ActivateAsync_TenantCreated_ActivatesAndSaves()
    {
        var tenant = NewTenant(TenantStatus.Created);
        _tenantRepository.Setup(r => r.GetByTenantIdAsync(tenant.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        _tenantRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var service = CreateService();
        await service.ActivateAsync(tenant.TenantId, CancellationToken.None);

        tenant.Status.ShouldBe(TenantStatus.Active);
        _tenantRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ActivateAsync_TenantAlreadyActive_IsIdempotentAndSkipsSave()
    {
        var tenant = NewTenant(TenantStatus.Active);
        _tenantRepository.Setup(r => r.GetByTenantIdAsync(tenant.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        var service = CreateService();
        await service.ActivateAsync(tenant.TenantId, CancellationToken.None);

        _tenantRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ActivateAsync_TenantNotFound_ThrowsNotFoundException()
    {
        _tenantRepository.Setup(r => r.GetByTenantIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);

        var service = CreateService();

        await Should.ThrowAsync<NotFoundException>(() => service.ActivateAsync("missing-tenant-id", CancellationToken.None));
    }

    [Fact]
    public async Task ExpireOverdueTenantsAsync_OverdueTenantsFound_ExpiresAllAndSavesOnce()
    {
        var overdueTenant1 = NewTenant(TenantStatus.Active);
        var overdueTenant2 = NewTenant(TenantStatus.Active);
        overdueTenant2.Id = 8;
        overdueTenant2.TenantId = "another-tenant-id";

        _tenantRepository.Setup(r => r.ListOverdueActiveTenantsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([overdueTenant1, overdueTenant2]);
        _tenantRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var service = CreateService();
        var expiredCount = await service.ExpireOverdueTenantsAsync(CancellationToken.None);

        expiredCount.ShouldBe(2);
        overdueTenant1.Status.ShouldBe(TenantStatus.Expired);
        overdueTenant2.Status.ShouldBe(TenantStatus.Expired);
        _tenantRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExpireOverdueTenantsAsync_NoOverdueTenants_SkipsSaveAndReturnsZero()
    {
        _tenantRepository.Setup(r => r.ListOverdueActiveTenantsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = CreateService();
        var expiredCount = await service.ExpireOverdueTenantsAsync(CancellationToken.None);

        expiredCount.ShouldBe(0);
        _tenantRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
