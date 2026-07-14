using AdminService.Application.DTOs;
using AdminService.Application.Exceptions;
using AdminService.Application.Interfaces;
using AdminService.Application.Services;
using AdminService.Domain.Entities;
using Moq;
using Shouldly;

namespace AdminService.UnitTests;

public class DatabaseInstanceServiceTests
{
    private readonly Mock<IDatabaseInstanceRepository> _repository = new();
    private readonly Mock<IDbCredentialCipher> _cipher = new();

    private DatabaseInstanceService CreateService() => new(_repository.Object, _cipher.Object);

    [Fact]
    public async Task CreateInstanceAsync_EncryptsPasswordBeforeStoring()
    {
        _repository.Setup(r => r.ExistsByNameAsync("prod-mysql", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _cipher.Setup(c => c.Encrypt("plaintext-pw")).Returns("cipher:plaintext-pw");

        DatabaseInstance? saved = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<DatabaseInstance>(), It.IsAny<CancellationToken>()))
            .Callback<DatabaseInstance, CancellationToken>((instance, _) => saved = instance)
            .Returns(Task.CompletedTask);

        var service = CreateService();
        var request = new CreateDatabaseInstanceRequest("prod-mysql", "mysql", "10.0.0.1", 3306, "app_user", "plaintext-pw");

        var result = await service.CreateInstanceAsync(request, CancellationToken.None);

        saved.ShouldNotBeNull();
        saved!.EncryptedPassword.ShouldBe("cipher:plaintext-pw");
        result.Name.ShouldBe("prod-mysql");
        result.DbType.ShouldBe("mysql");
        _cipher.Verify(c => c.Encrypt("plaintext-pw"), Times.Once);
    }

    [Fact]
    public async Task CreateInstanceAsync_DuplicateName_ThrowsValidationException()
    {
        _repository.Setup(r => r.ExistsByNameAsync("prod-mysql", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService();
        var request = new CreateDatabaseInstanceRequest("prod-mysql", "mysql", "10.0.0.1", 3306, "app_user", "pw");

        await Should.ThrowAsync<ValidationException>(() => service.CreateInstanceAsync(request, CancellationToken.None));
        _cipher.Verify(c => c.Encrypt(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("postgres")]
    [InlineData("")]
    [InlineData("mongodb")]
    public async Task CreateInstanceAsync_UnsupportedDbType_ThrowsValidationException(string dbType)
    {
        var service = CreateService();
        var request = new CreateDatabaseInstanceRequest("name", dbType, "10.0.0.1", 3306, "app_user", "pw");

        await Should.ThrowAsync<ValidationException>(() => service.CreateInstanceAsync(request, CancellationToken.None));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(70000)]
    public async Task CreateInstanceAsync_InvalidPort_ThrowsValidationException(int port)
    {
        var service = CreateService();
        var request = new CreateDatabaseInstanceRequest("name", "mysql", "10.0.0.1", port, "app_user", "pw");

        await Should.ThrowAsync<ValidationException>(() => service.CreateInstanceAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task CreateInstanceAsync_MissingPassword_ThrowsValidationException()
    {
        var service = CreateService();
        var request = new CreateDatabaseInstanceRequest("name", "mysql", "10.0.0.1", 3306, "app_user", "");

        await Should.ThrowAsync<ValidationException>(() => service.CreateInstanceAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task GetInstanceAsync_NotFound_ThrowsNotFoundException()
    {
        _repository.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DatabaseInstance?)null);

        var service = CreateService();

        await Should.ThrowAsync<NotFoundException>(() => service.GetInstanceAsync(99, CancellationToken.None));
    }

    [Fact]
    public async Task GetInstanceAsync_DoesNotExposePasswordInResponse()
    {
        var instance = new DatabaseInstance
        {
            Id = 1,
            Name = "prod-mysql",
            DbType = DatabaseInstanceType.MySql,
            Host = "10.0.0.1",
            Port = 3306,
            Username = "app_user",
            EncryptedPassword = "cipher:secret",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _repository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(instance);

        var service = CreateService();
        var result = await service.GetInstanceAsync(1, CancellationToken.None);

        // DatabaseInstanceResponse 类型上本就没有密码字段，这里断言不出现在序列化会用到的属性集合里
        typeof(DatabaseInstanceResponse).GetProperties().ShouldNotContain(p => p.Name.Contains("Password"));
    }

    [Fact]
    public async Task UpdateInstanceAsync_WithoutPassword_KeepsExistingEncryptedPassword()
    {
        var instance = new DatabaseInstance
        {
            Id = 1,
            Name = "prod-mysql",
            DbType = DatabaseInstanceType.MySql,
            Host = "10.0.0.1",
            Port = 3306,
            Username = "app_user",
            EncryptedPassword = "cipher:old-secret",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _repository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        _repository.Setup(r => r.ExistsByNameAsync("prod-mysql", 1, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var service = CreateService();
        var request = new UpdateDatabaseInstanceRequest("prod-mysql", "10.0.0.2", 3307, "app_user2", null);

        await service.UpdateInstanceAsync(1, request, CancellationToken.None);

        instance.EncryptedPassword.ShouldBe("cipher:old-secret");
        instance.Host.ShouldBe("10.0.0.2");
        instance.Username.ShouldBe("app_user2");
        _cipher.Verify(c => c.Encrypt(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateInstanceAsync_WithNewPassword_ReEncrypts()
    {
        var instance = new DatabaseInstance
        {
            Id = 1,
            Name = "prod-mysql",
            DbType = DatabaseInstanceType.MySql,
            Host = "10.0.0.1",
            Port = 3306,
            Username = "app_user",
            EncryptedPassword = "cipher:old-secret",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _repository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        _repository.Setup(r => r.ExistsByNameAsync("prod-mysql", 1, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _cipher.Setup(c => c.Encrypt("new-secret")).Returns("cipher:new-secret");

        var service = CreateService();
        var request = new UpdateDatabaseInstanceRequest("prod-mysql", "10.0.0.1", 3306, "app_user", "new-secret");

        await service.UpdateInstanceAsync(1, request, CancellationToken.None);

        instance.EncryptedPassword.ShouldBe("cipher:new-secret");
    }

    [Fact]
    public async Task UpdateInstanceAsync_NotFound_ThrowsNotFoundException()
    {
        _repository.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((DatabaseInstance?)null);

        var service = CreateService();
        var request = new UpdateDatabaseInstanceRequest("name", "host", 3306, "user", null);

        await Should.ThrowAsync<NotFoundException>(() => service.UpdateInstanceAsync(99, request, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteInstanceAsync_NotFound_ThrowsNotFoundException()
    {
        _repository.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((DatabaseInstance?)null);

        var service = CreateService();

        await Should.ThrowAsync<NotFoundException>(() => service.DeleteInstanceAsync(99, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteInstanceAsync_Found_DeletesViaRepository()
    {
        var instance = new DatabaseInstance { Id = 1, Name = "prod-mysql" };
        _repository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(instance);

        var service = CreateService();
        await service.DeleteInstanceAsync(1, CancellationToken.None);

        _repository.Verify(r => r.DeleteAsync(instance, It.IsAny<CancellationToken>()), Times.Once);
    }
}
