using AdminService.Domain.Entities;

namespace AdminService.Application.DTOs;

/// <summary>密码字段有意不出现在这里：数据库实例密码只落库（加密后），不通过任何查询接口回显。</summary>
public class DatabaseInstanceResponse
{
    public long Id { get; init; }
    public required string Name { get; init; }
    public required string DbType { get; init; }
    public required string Host { get; init; }
    public int Port { get; init; }
    public required string Username { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }

    public static DatabaseInstanceResponse FromEntity(DatabaseInstance instance) => new()
    {
        Id = instance.Id,
        Name = instance.Name,
        DbType = instance.DbType.ToString().ToLowerInvariant(),
        Host = instance.Host,
        Port = instance.Port,
        Username = instance.Username,
        CreatedAt = instance.CreatedAt,
        UpdatedAt = instance.UpdatedAt,
    };
}

public record CreateDatabaseInstanceRequest(
    string Name,
    string DbType,
    string Host,
    int Port,
    string Username,
    string Password);

public record UpdateDatabaseInstanceRequest(
    string Name,
    string Host,
    int Port,
    string Username,
    string? Password);
