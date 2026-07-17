using MySqlConnector;
using StackExchange.Redis;

namespace TenantDbConnector;

/// <summary>
/// <see cref="ITenantDbConnector"/> 的默认实现。应通过 DI 注册为单例
/// （services.AddSingleton&lt;ITenantDbConnector&gt;(...)），进程生命周期内复用同一个实例。
/// </summary>
public sealed class TenantDbConnectorImpl : ITenantDbConnector
{
    private readonly TenantInfoCache _infoCache;
    private readonly DataSourceRegistry _dataSources;
    private readonly Func<TenantDbInfo, TenantDbConnectorOptions, string> _connectionStringBuilder;
    private readonly TenantDbConnectorOptions _options;
    private int _disposed;

    /// <param name="redis">
    /// 可为 null，此时跳过 Redis 层，只用本地缓存 + SsoGetter 回源，适合本地开发/测试场景。
    /// </param>
    /// <param name="ssoGetter">必填：如何从 sso-service 拉取租户数据库连接信息。</param>
    /// <param name="options">未提供则使用默认配置。</param>
    public TenantDbConnectorImpl(IConnectionMultiplexer? redis, SsoGetter ssoGetter, TenantDbConnectorOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(ssoGetter);

        _options = options ?? new TenantDbConnectorOptions();
        _connectionStringBuilder = _options.ConnectionStringBuilder ?? DefaultConnectionStringBuilder.Build;
        _infoCache = new TenantInfoCache(redis, ssoGetter, _options.LocalCacheTtl, _options.RedisCacheTtl, _options.RedisKeyPrefix);
        _dataSources = new DataSourceRegistry(_options.IdlePoolTtl, _options.IdleSweepInterval);
    }

    public async Task<TenantDbInfo> GetTenantDbInfoAsync(string tenantCode, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return await _infoCache.GetAsync(tenantCode, ct).ConfigureAwait(false);
    }

    public async Task<MySqlDataSource> GetDataSourceAsync(string tenantCode, CancellationToken ct = default)
    {
        ThrowIfDisposed();

        var info = await _infoCache.GetAsync(tenantCode, ct).ConfigureAwait(false);
        var connectionString = _connectionStringBuilder(info, _options);
        return _dataSources.GetOrAdd(tenantCode, connectionString);
    }

    public async Task InvalidateTenantDbInfoAsync(string tenantCode, CancellationToken ct = default)
    {
        ThrowIfDisposed();

        await _infoCache.InvalidateAsync(tenantCode, ct).ConfigureAwait(false);
        await _dataSources.EvictAsync(tenantCode).ConfigureAwait(false);
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(TenantDbConnectorImpl));
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _dataSources.DisposeAsync().ConfigureAwait(false);
        await _infoCache.DisposeAsync().ConfigureAwait(false);
    }
}
