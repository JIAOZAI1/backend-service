namespace TenantDbConnector;

/// <summary>
/// 表示 tenantCode 不存在。<see cref="SsoGetter"/> 实现应在这种情况下抛出本异常，
/// 便于上层区分"租户不存在"与其他瞬时故障。
/// </summary>
public sealed class TenantNotFoundException(string tenantCode)
    : Exception($"tenant not found: {tenantCode}")
{
    public string TenantCode { get; } = tenantCode;
}
