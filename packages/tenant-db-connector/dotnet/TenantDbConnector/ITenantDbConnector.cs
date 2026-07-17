using MySqlConnector;

namespace TenantDbConnector;

/// <summary>
/// 供需要直连租户数据库的服务（如未来的租户服务）按 tenantCode 查询数据库连接信息
/// 并获取常驻连接池句柄。应通过 DI 注册为单例，进程生命周期内复用同一个实例，
/// 不要每次请求都 new 一个。
/// </summary>
public interface ITenantDbConnector : IAsyncDisposable
{
    /// <summary>
    /// 返回租户数据库连接信息：本地缓存 → Redis 缓存 → SsoGetter 回源
    /// （命中/回源结果按各自 TTL 写回两级缓存）。
    /// </summary>
    Task<TenantDbInfo> GetTenantDbInfoAsync(string tenantCode, CancellationToken ct = default);

    /// <summary>
    /// 返回该租户的 <see cref="MySqlDataSource"/>（连接池句柄）。同一 tenantCode 在进程
    /// 生命周期内复用同一个数据源，不会每次调用都新建连接池。若已有连接池但底层连接因
    /// 密码轮换等原因失效，调用方应捕获连接错误后调用 <see cref="InvalidateTenantDbInfoAsync"/>
    /// 强制重建。
    /// </summary>
    Task<MySqlDataSource> GetDataSourceAsync(string tenantCode, CancellationToken ct = default);

    /// <summary>
    /// 清除该租户的缓存信息并释放其现有连接池（如果存在）。用于密码轮换等运维操作后
    /// 强制下一次查询回源重建，不必等待缓存 TTL 自然过期。
    /// </summary>
    Task InvalidateTenantDbInfoAsync(string tenantCode, CancellationToken ct = default);
}
