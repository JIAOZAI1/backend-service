using AdminService.Application.Common;
using AdminService.Application.DTOs;
using AdminService.Application.Exceptions;
using AdminService.Application.Interfaces;
using AdminService.Domain.Entities;

namespace AdminService.Application.Services;

public class DatabaseInstanceService(
    IDatabaseInstanceRepository repository,
    IDbCredentialCipher cipher) : IDatabaseInstanceService
{
    private static readonly IReadOnlySet<string> _sortFields =
        new HashSet<string>(["id", "name", "dbType", "createdAt", "updatedAt"], StringComparer.OrdinalIgnoreCase);

    public async Task<PagedResult<DatabaseInstanceResponse>> ListInstancesAsync(
        int page, int pageSize, string? sortBy, SortOrder sortOrder, CancellationToken cancellationToken)
    {
        var sort = SortSpec.Resolve(sortBy, sortOrder, _sortFields, defaultField: "id");
        var (items, total) = await repository.ListPagedAsync(page, pageSize, sort, cancellationToken);
        return new PagedResult<DatabaseInstanceResponse>
        {
            Items = items.Select(DatabaseInstanceResponse.FromEntity).ToList(),
            Page = page,
            PageSize = pageSize,
            Total = total,
        };
    }

    public async Task<DatabaseInstanceResponse> GetInstanceAsync(long id, CancellationToken cancellationToken)
    {
        var instance = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"database instance '{id}' not found");
        return DatabaseInstanceResponse.FromEntity(instance);
    }

    public async Task<DatabaseInstanceResponse> CreateInstanceAsync(
        CreateDatabaseInstanceRequest request, CancellationToken cancellationToken)
    {
        var dbType = ParseDbType(request.DbType);
        ValidatePort(request.Port);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException("name is required");
        }
        if (string.IsNullOrEmpty(request.Password))
        {
            throw new ValidationException("password is required");
        }
        if (await repository.ExistsByNameAsync(request.Name, excludingId: null, cancellationToken))
        {
            throw new ValidationException($"database instance name '{request.Name}' already exists");
        }

        var now = DateTime.UtcNow;
        var instance = new DatabaseInstance
        {
            Name = request.Name,
            DbType = dbType,
            Host = request.Host,
            Port = request.Port,
            Username = request.Username,
            EncryptedPassword = cipher.Encrypt(request.Password),
            CreatedAt = now,
            UpdatedAt = now,
        };

        await repository.AddAsync(instance, cancellationToken);
        return DatabaseInstanceResponse.FromEntity(instance);
    }

    public async Task<DatabaseInstanceResponse> UpdateInstanceAsync(
        long id, UpdateDatabaseInstanceRequest request, CancellationToken cancellationToken)
    {
        var instance = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"database instance '{id}' not found");

        ValidatePort(request.Port);
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException("name is required");
        }
        if (await repository.ExistsByNameAsync(request.Name, excludingId: id, cancellationToken))
        {
            throw new ValidationException($"database instance name '{request.Name}' already exists");
        }

        instance.Name = request.Name;
        instance.Host = request.Host;
        instance.Port = request.Port;
        instance.Username = request.Username;
        if (!string.IsNullOrEmpty(request.Password))
        {
            instance.EncryptedPassword = cipher.Encrypt(request.Password);
        }
        instance.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(instance, cancellationToken);
        return DatabaseInstanceResponse.FromEntity(instance);
    }

    public async Task DeleteInstanceAsync(long id, CancellationToken cancellationToken)
    {
        var instance = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"database instance '{id}' not found");
        await repository.DeleteAsync(instance, cancellationToken);
    }

    private static DatabaseInstanceType ParseDbType(string dbType)
    {
        if (!Enum.TryParse<DatabaseInstanceType>(dbType, ignoreCase: true, out var parsed))
        {
            throw new ValidationException($"unsupported dbType: {dbType}. allowed values: mysql");
        }
        return parsed;
    }

    private static void ValidatePort(int port)
    {
        if (port is <= 0 or > 65535)
        {
            throw new ValidationException($"invalid port: {port}");
        }
    }
}
