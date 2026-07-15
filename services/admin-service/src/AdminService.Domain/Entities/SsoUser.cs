namespace AdminService.Domain.Entities;

/// <summary>
/// users 表由 sso-service 拥有（迁移脚本以 services/sso-service/migrations 为准），
/// 本服务与 sso-service 共用同一个 MySQL 数据库（sys_db），因此这里直接映射同一张表做
/// 跨服务查询/密码重置，不通过 HTTP 调用 sso-service。字段只声明本服务用得到的列，
/// 不是 sso-service User 模型的完整镜像。命名为 SsoUser 而非 User，避免与本服务
/// GatewayUser（网关注入的当前登录管理员身份）混淆。
/// </summary>
public class SsoUser
{
    public ulong Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
