using AdminService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdminService.Infrastructure.Persistence.Configurations;

public class UserTenantConfiguration : IEntityTypeConfiguration<UserTenant>
{
    public void Configure(EntityTypeBuilder<UserTenant> builder)
    {
        builder.ToTable("user_tenants");

        builder.HasKey(ut => ut.Id);
        builder.Property(ut => ut.Id).HasColumnName("id");

        builder.Property(ut => ut.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(ut => ut.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(ut => ut.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(ut => ut.DeletedAt).HasColumnName("deleted_at");

        builder.HasIndex(ut => new { ut.UserId, ut.TenantId })
            .HasDatabaseName("uk_user_tenants_user_tenant").IsUnique();
        builder.HasIndex(ut => ut.DeletedAt).HasDatabaseName("idx_user_tenants_deleted_at");
        builder.HasQueryFilter(ut => ut.DeletedAt == null);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(ut => ut.TenantId)
            .HasConstraintName("fk_user_tenants_tenant_id");
    }
}
