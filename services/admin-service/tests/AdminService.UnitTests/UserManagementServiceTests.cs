using AdminService.Application.Common;
using AdminService.Application.Exceptions;
using AdminService.Application.Interfaces;
using AdminService.Application.Services;
using AdminService.Domain.Entities;
using Moq;
using Shouldly;

namespace AdminService.UnitTests;

public class UserManagementServiceTests
{
    private readonly Mock<IUserManagementRepository> _repository = new();

    private UserManagementService CreateService() => new(_repository.Object);

    [Fact]
    public async Task ListUsersAsync_MapsRowsToResponseIncludingTenantInfo()
    {
        var rows = new List<UserWithTenantRow>
        {
            new(1, "alice", "alice@example.com", ["admin", "default"], true, "abcd1234wxyz", DateTime.UtcNow.AddMonths(6), DateTime.UtcNow),
            new(2, "bob", "bob@example.com", ["default"], false, null, null, DateTime.UtcNow),
        };
        _repository.Setup(r => r.ListUsersWithTenantAsync(1, 20, It.IsAny<SortSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((rows, 2L));

        var service = CreateService();
        var result = await service.ListUsersAsync(1, 20, sortBy: null, SortOrder.Asc, CancellationToken.None);

        result.Total.ShouldBe(2);
        result.Items.Count.ShouldBe(2);

        var alice = result.Items.Single(u => u.Username == "alice");
        alice.Roles.ShouldBe(["admin", "default"]);
        alice.TenantCode.ShouldBe("abcd1234wxyz");
        alice.LicenseExpiresAt.ShouldNotBeNull();

        var bob = result.Items.Single(u => u.Username == "bob");
        bob.TenantCode.ShouldBeNull();
        bob.LicenseExpiresAt.ShouldBeNull();
    }

    [Fact]
    public async Task ListUsersAsync_InvalidSortBy_ThrowsValidationException()
    {
        var service = CreateService();

        await Should.ThrowAsync<ValidationException>(
            () => service.ListUsersAsync(1, 20, "notAField", SortOrder.Asc, CancellationToken.None));

        _repository.Verify(r => r.ListUsersWithTenantAsync(
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<SortSpec>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResetPasswordAsync_UserExists_GeneratesNewBcryptHashAndSaves()
    {
        var user = new SsoUser { Id = 42, Username = "alice", Email = "alice@example.com", PasswordHash = "old-hash" };
        _repository.Setup(r => r.GetUserByIdAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _repository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var service = CreateService();
        var result = await service.ResetPasswordAsync(42, CancellationToken.None);

        result.NewPassword.ShouldNotBeNullOrWhiteSpace();
        result.NewPassword.Length.ShouldBe(16);
        user.PasswordHash.ShouldNotBe("old-hash");
        // 新密码的 bcrypt 密文应能验证回原始明文
        BCrypt.Net.BCrypt.Verify(result.NewPassword, user.PasswordHash).ShouldBeTrue();
        _repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetPasswordAsync_UserNotFound_ThrowsNotFoundException()
    {
        _repository.Setup(r => r.GetUserByIdAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SsoUser?)null);

        var service = CreateService();

        await Should.ThrowAsync<NotFoundException>(() => service.ResetPasswordAsync(999, CancellationToken.None));
        _repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetUserAsync_UserExists_MapsEnabledFromStatus()
    {
        var user = new SsoUser { Id = 42, Username = "alice", Email = "alice@example.com", Status = SsoUserStatus.Active };
        _repository.Setup(r => r.GetUserByIdAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var service = CreateService();
        var result = await service.GetUserAsync(42, CancellationToken.None);

        result.Username.ShouldBe("alice");
        result.Enabled.ShouldBeTrue();
    }

    [Fact]
    public async Task GetUserAsync_UserNotFound_ThrowsNotFoundException()
    {
        _repository.Setup(r => r.GetUserByIdAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SsoUser?)null);

        var service = CreateService();

        await Should.ThrowAsync<NotFoundException>(() => service.GetUserAsync(999, CancellationToken.None));
    }

    [Fact]
    public async Task SetUserEnabledAsync_Disable_SetsStatusToDisabledAndSaves()
    {
        var user = new SsoUser { Id = 42, Username = "alice", Email = "alice@example.com", Status = SsoUserStatus.Active };
        _repository.Setup(r => r.GetUserByIdAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _repository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var service = CreateService();
        await service.SetUserEnabledAsync(42, enabled: false, CancellationToken.None);

        user.Status.ShouldBe(SsoUserStatus.Disabled);
        _repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetUserEnabledAsync_Enable_SetsStatusToActiveAndSaves()
    {
        var user = new SsoUser { Id = 42, Username = "alice", Email = "alice@example.com", Status = SsoUserStatus.Disabled };
        _repository.Setup(r => r.GetUserByIdAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _repository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var service = CreateService();
        await service.SetUserEnabledAsync(42, enabled: true, CancellationToken.None);

        user.Status.ShouldBe(SsoUserStatus.Active);
        _repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetUserEnabledAsync_UserNotFound_ThrowsNotFoundException()
    {
        _repository.Setup(r => r.GetUserByIdAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SsoUser?)null);

        var service = CreateService();

        await Should.ThrowAsync<NotFoundException>(
            () => service.SetUserEnabledAsync(999, enabled: false, CancellationToken.None));
        _repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
