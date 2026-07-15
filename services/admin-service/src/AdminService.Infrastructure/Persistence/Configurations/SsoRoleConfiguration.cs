using AdminService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdminService.Infrastructure.Persistence.Configurations;

public class SsoRoleConfiguration : IEntityTypeConfiguration<SsoRole>
{
    public void Configure(EntityTypeBuilder<SsoRole> builder)
    {
        builder.ToTable("roles");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.Name).HasColumnName("name").HasMaxLength(64).IsRequired();
    }
}

public class SsoUserRoleConfiguration : IEntityTypeConfiguration<SsoUserRole>
{
    public void Configure(EntityTypeBuilder<SsoUserRole> builder)
    {
        builder.ToTable("user_roles");
        builder.HasKey(ur => ur.Id);
        builder.Property(ur => ur.Id).HasColumnName("id");
        builder.Property(ur => ur.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(ur => ur.RoleId).HasColumnName("role_id").IsRequired();
    }
}
