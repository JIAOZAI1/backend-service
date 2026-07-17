namespace TenantDbConnector;

/// <summary>
/// <see cref="TenantDbConnectorImpl"/> 的可配置项。未显式设置的字段使用默认值。
/// </summary>
public sealed class TenantDbConnectorOptions
{
    /// <summary>本地进程内缓存 TTL：远短于 Redis TTL，只用来吸收突发流量对 Redis 的压力。</summary>
    public TimeSpan LocalCacheTtl { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Redis 缓存 TTL，与 sso-service 侧的缓存 TTL 对齐。</summary>
    public TimeSpan RedisCacheTtl { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>租户连接池的空闲回收阈值：超过此时长未被访问的连接池会被自动释放。</summary>
    public TimeSpan IdlePoolTtl { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>空闲连接池清理的巡检周期。</summary>
    public TimeSpan IdleSweepInterval { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Redis key 前缀，与 sso-service 自身的缓存前缀区分开，避免 key 冲突。</summary>
    public string RedisKeyPrefix { get; init; } = "tenant-db-connector:info:";

    /// <summary>每个租户连接池的最大连接数（MySqlConnectionStringBuilder.MaximumPoolSize）。</summary>
    public uint MaxPoolSize { get; init; } = 10;

    /// <summary>每个租户连接池的最小连接数（MySqlConnectionStringBuilder.MinimumPoolSize）。</summary>
    public uint MinPoolSize { get; init; } = 0;

    /// <summary>TenantDbInfo → MySQL 连接串的拼接方式，预留给自定义扩展（如追加额外连接参数）。</summary>
    public Func<TenantDbInfo, TenantDbConnectorOptions, string>? ConnectionStringBuilder { get; init; }
}
