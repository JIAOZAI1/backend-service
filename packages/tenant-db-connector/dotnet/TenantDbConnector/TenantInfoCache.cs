using System.Collections.Concurrent;
using System.Text.Json;
using StackExchange.Redis;

namespace TenantDbConnector;

/// <summary>
/// 实现 TenantDbInfo 的三级 cache-aside 查询：本地内存 → Redis → SsoGetter 回源。
/// 同一 tenantCode 的并发回源请求通过按 key 的 SemaphoreSlim 合并为一次，避免缓存击穿时
/// 打爆 sso-service。
/// </summary>
internal sealed class TenantInfoCache : IAsyncDisposable
{
    private readonly IConnectionMultiplexer? _redis;
    private readonly SsoGetter _ssoGetter;
    private readonly TimeSpan _localTtl;
    private readonly TimeSpan _redisTtl;
    private readonly string _redisKeyPrefix;

    private readonly ConcurrentDictionary<string, (TenantDbInfo Info, DateTimeOffset ExpiresAt)> _local = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public TenantInfoCache(IConnectionMultiplexer? redis, SsoGetter ssoGetter, TimeSpan localTtl, TimeSpan redisTtl, string redisKeyPrefix)
    {
        _redis = redis;
        _ssoGetter = ssoGetter;
        _localTtl = localTtl;
        _redisTtl = redisTtl;
        _redisKeyPrefix = redisKeyPrefix;
    }

    public async Task<TenantDbInfo> GetAsync(string tenantCode, CancellationToken ct)
    {
        if (TryGetLocal(tenantCode, out var cached))
        {
            return cached;
        }

        var gate = _locks.GetOrAdd(tenantCode, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Re-check after acquiring the gate: a concurrent caller may have just populated it.
            if (TryGetLocal(tenantCode, out cached))
            {
                return cached;
            }

            return await LoadAsync(tenantCode, ct).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<TenantDbInfo> LoadAsync(string tenantCode, CancellationToken ct)
    {
        var fromRedis = await TryGetRedisAsync(tenantCode, ct).ConfigureAwait(false);
        if (fromRedis is { } info)
        {
            SetLocal(tenantCode, info);
            return info;
        }

        var fetched = await _ssoGetter(tenantCode, ct).ConfigureAwait(false);

        SetLocal(tenantCode, fetched);
        await TrySetRedisAsync(tenantCode, fetched, ct).ConfigureAwait(false); // Redis write failure doesn't block returning the freshly-fetched value

        return fetched;
    }

    private bool TryGetLocal(string tenantCode, out TenantDbInfo info)
    {
        if (_local.TryGetValue(tenantCode, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow)
        {
            info = entry.Info;
            return true;
        }
        info = default!;
        return false;
    }

    private void SetLocal(string tenantCode, TenantDbInfo info)
        => _local[tenantCode] = (info, DateTimeOffset.UtcNow.Add(_localTtl));

    private async Task<TenantDbInfo?> TryGetRedisAsync(string tenantCode, CancellationToken ct)
    {
        if (_redis is null)
        {
            return null;
        }

        var db = _redis.GetDatabase();
        var val = await db.StringGetAsync(_redisKeyPrefix + tenantCode).WaitAsync(ct).ConfigureAwait(false);
        if (val.IsNullOrEmpty)
        {
            return null;
        }

        return JsonSerializer.Deserialize<TenantDbInfo>((string)val!);
    }

    private async Task TrySetRedisAsync(string tenantCode, TenantDbInfo info, CancellationToken ct)
    {
        if (_redis is null)
        {
            return;
        }

        var db = _redis.GetDatabase();
        var json = JsonSerializer.Serialize(info);
        await db.StringSetAsync(_redisKeyPrefix + tenantCode, json, _redisTtl).WaitAsync(ct).ConfigureAwait(false);
    }

    public async Task InvalidateAsync(string tenantCode, CancellationToken ct)
    {
        _local.TryRemove(tenantCode, out _);

        if (_redis is null)
        {
            return;
        }

        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(_redisKeyPrefix + tenantCode).WaitAsync(ct).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        foreach (var gate in _locks.Values)
        {
            gate.Dispose();
        }
        return ValueTask.CompletedTask;
    }
}
