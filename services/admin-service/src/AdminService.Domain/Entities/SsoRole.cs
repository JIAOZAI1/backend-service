namespace AdminService.Domain.Entities;

/// <summary>roles/user_roles 表由 sso-service 拥有，本服务只读，见 SsoUser.cs 顶部说明。</summary>
public class SsoRole
{
    public ulong Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>user_roles 关联表，由 sso-service 拥有，本服务只读。</summary>
public class SsoUserRole
{
    public ulong Id { get; set; }
    public ulong UserId { get; set; }
    public ulong RoleId { get; set; }
}
