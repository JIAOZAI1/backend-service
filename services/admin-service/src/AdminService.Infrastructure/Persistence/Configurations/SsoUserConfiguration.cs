using AdminService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdminService.Infrastructure.Persistence.Configurations;

public class SsoUserConfiguration : IEntityTypeConfiguration<SsoUser>
{
    public void Configure(EntityTypeBuilder<SsoUser> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id");

        builder.Property(u => u.Username).HasColumnName("username").HasMaxLength(64).IsRequired();
        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(128).IsRequired();
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsRequired();
        builder.Property(u => u.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(u => u.DeletedAt).HasColumnName("deleted_at");

        // 与 sso-service 的软删除语义保持一致：拒绝审核的用户会被软删除（见 sso-service README
        // "用户审核字段说明"），这里同样排除，不在用户列表/密码重置里出现已拒绝用户。
        builder.HasQueryFilter(u => u.DeletedAt == null);
    }
}
