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
    public ulong ReviewedBy { get; set; }
    public TenantStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
