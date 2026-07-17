namespace TenantDbConnector;

/// <summary>
/// 某个租户的数据库连接信息，字段对应 sso-service
/// GET /internal/tenants/{tenantCode}/db-info 的响应体。DbPassword 为明文，
/// 调用方与本 SDK 都不应将其写入日志。
/// </summary>
public sealed record TenantDbInfo(
    string TenantCode,
    string DbHost,
    int DbPort,
    string DbName,
    string DbUsername,
    string DbPassword);
