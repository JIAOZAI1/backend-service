using AdminService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdminService.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");

        builder.Property(t => t.TenantId).HasColumnName("tenant_id").HasMaxLength(36).IsRequired();
        builder.Property(t => t.TenantCode).HasColumnName("tenant_code").HasMaxLength(32).IsRequired();
        builder.Property(t => t.DbType).HasColumnName("db_type").HasMaxLength(32).IsRequired();
        builder.Property(t => t.DbHost).HasColumnName("db_host").HasMaxLength(255).IsRequired();
        builder.Property(t => t.DbPort).HasColumnName("db_port").IsRequired();
        builder.Property(t => t.DbName).HasColumnName("db_name").HasMaxLength(64).IsRequired();
        builder.Property(t => t.DbUsername).HasColumnName("db_username").HasMaxLength(32).IsRequired();
        builder.Property(t => t.DatabaseInstanceId).HasColumnName("database_instance_id");
        builder.Property(t => t.DbPassword).HasColumnName("db_password").HasMaxLength(255).IsRequired();
        builder.Property(t => t.ReviewedBy).HasColumnName("reviewed_by").IsRequired();
        builder.Property(t => t.Status).HasColumnName("status")
            .HasConversion(
                status => status.ToString().ToLowerInvariant(),
                value => Enum.Parse<TenantStatus>(value, ignoreCase: true))
            .HasMaxLength(16).IsRequired();
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(t => t.DeletedAt).HasColumnName("deleted_at");

        builder.HasIndex(t => t.TenantId).HasDatabaseName("uk_tenants_tenant_id").IsUnique();
        builder.HasIndex(t => t.TenantCode).HasDatabaseName("uk_tenants_tenant_code").IsUnique();
        builder.HasIndex(t => t.DeletedAt).HasDatabaseName("idx_tenants_deleted_at");
        builder.HasIndex(t => t.Status).HasDatabaseName("idx_tenants_status");
        builder.HasIndex(t => t.DatabaseInstanceId).HasDatabaseName("idx_tenants_database_instance_id");
        builder.HasQueryFilter(t => t.DeletedAt == null);
    }
}
