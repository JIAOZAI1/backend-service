namespace TenantDbConnector;

/// <summary>
/// 由调用方实现：如何从 sso-service 拉取指定 tenantCode 的数据库连接信息
/// （host、X-Internal-Token 等由调用方从自己的配置注入，SDK 不关心网络细节）。
/// tenantCode 不存在时应抛出 <see cref="TenantNotFoundException"/>。
/// </summary>
public delegate Task<TenantDbInfo> SsoGetter(string tenantCode, CancellationToken ct);
