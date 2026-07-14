namespace AdminService.Domain.Entities;

public class UserTenant
{
    public long Id { get; set; }
    public ulong UserId { get; set; }
    public long TenantId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
