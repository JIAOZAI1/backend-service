namespace AdminService.Domain.Entities;

public enum DatabaseInstanceType
{
    MySql = 1,
}

/// <summary>
/// 管理员注册的数据库实例：供作业场景（backend-job-service）选择目标实例执行作业。
/// 密码只以 <see cref="EncryptedPassword"/> 密文形式落库，解密由基础设施层
/// （<see cref="AdminService.Application.Interfaces.IDbCredentialCipher"/>）在需要时按需进行。
/// </summary>
public class DatabaseInstance
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DatabaseInstanceType DbType { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Username { get; set; } = string.Empty;
    public string EncryptedPassword { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
