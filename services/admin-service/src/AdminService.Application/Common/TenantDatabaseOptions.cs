namespace AdminService.Application.Common;

/// <summary>新租户数据库实际落在的目标 MySQL 实例地址，从配置 TenantDatabase:Host/Port/Type 注入。</summary>
public class TenantDatabaseOptions
{
    public required string DbType { get; init; }
    public required string Host { get; init; }
    public required int Port { get; init; }
}
