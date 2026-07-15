namespace AdminService.Domain.Entities;

public enum TenantStatus
{
    Created = 1,
    Active = 2,
    Expired = 3,
    Cancelled = 4,
}

public class Tenant
{
    public long Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string TenantCode { get; set; } = string.Empty;
    public string DbType { get; set; } = string.Empty;
    public string DbHost { get; set; } = string.Empty;
    public int DbPort { get; set; }
    public string DbName { get; set; } = string.Empty;
    public string DbUsername { get; set; } = string.Empty;
    public string DbPassword { get; set; } = string.Empty;

    /// <summary>创建时选中的 DatabaseInstance.Id，可空——本字段引入前创建的租户记录没有这个信息。
    /// 供后续开户初始化作业（如未来的 init-tenant-schema）追溯该租户库落在哪个已登记实例上。</summary>
    public long? DatabaseInstanceId { get; set; }
    public ulong ReviewedBy { get; set; }
    public TenantStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
