using System.Collections.Concurrent;
using MySqlConnector;

namespace TenantDbConnector;

/// <summary>
/// 按 tenantCode 维护常驻的 <see cref="MySqlDataSource"/>（连接池句柄），同一租户的并发
/// GetAsync 调用复用同一个数据源，不会重复建池导致连接数暴涨。长期未被访问的数据源由
/// 后台巡检定期释放回收，避免租户数量增长后占满下游 MySQL 的 max_connections。
/// </summary>
internal sealed class DataSourceRegistry : IAsyncDisposable
{
    private sealed record Entry(MySqlDataSource DataSource, DateTimeOffset LastUsedAt)
    {
        public DateTimeOffset LastUsedAt { get; set; } = LastUsedAt;
    }

    private readonly ConcurrentDictionary<string, Entry> _entries = new();
    private readonly object _createLock = new();
    private readonly TimeSpan _idleTtl;
    private readonly Timer _sweepTimer;

    public DataSourceRegistry(TimeSpan idleTtl, TimeSpan sweepInterval)
    {
        _idleTtl = idleTtl;
        _sweepTimer = new Timer(_ => SweepIdle(), null, sweepInterval, sweepInterval);
    }

    public MySqlDataSource GetOrAdd(string tenantCode, string connectionString)
    {
        if (_entries.TryGetValue(tenantCode, out var existing))
        {
            existing.LastUsedAt = DateTimeOffset.UtcNow;
            return existing.DataSource;
        }

        lock (_createLock)
        {
            if (_entries.TryGetValue(tenantCode, out existing))
            {
                existing.LastUsedAt = DateTimeOffset.UtcNow;
                return existing.DataSource;
            }

            var dataSource = new MySqlDataSourceBuilder(connectionString).Build();
            _entries[tenantCode] = new Entry(dataSource, DateTimeOffset.UtcNow);
            return dataSource;
        }
    }

    public async Task EvictAsync(string tenantCode)
    {
        if (_entries.TryRemove(tenantCode, out var entry))
        {
            await entry.DataSource.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void SweepIdle()
    {
        var cutoff = DateTimeOffset.UtcNow - _idleTtl;
        foreach (var (tenantCode, entry) in _entries)
        {
            if (entry.LastUsedAt < cutoff && _entries.TryRemove(tenantCode, out var removed))
            {
                _ = removed.DataSource.DisposeAsync().AsTask();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _sweepTimer.DisposeAsync().ConfigureAwait(false);

        foreach (var tenantCode in _entries.Keys.ToArray())
        {
            if (_entries.TryRemove(tenantCode, out var entry))
            {
                await entry.DataSource.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
