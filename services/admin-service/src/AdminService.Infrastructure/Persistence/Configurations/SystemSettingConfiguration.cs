using AdminService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdminService.Infrastructure.Persistence.Configurations;

public class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.ToTable("system_settings");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");

        builder.Property(s => s.Key).HasColumnName("key").HasMaxLength(128).IsRequired();
        builder.Property(s => s.Value).HasColumnName("value").HasMaxLength(2048).IsRequired();
        builder.Property(s => s.Description).HasColumnName("description").HasMaxLength(512).IsRequired();
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(s => s.DeletedAt).HasColumnName("deleted_at");

        builder.HasIndex(s => s.Key).HasDatabaseName("idx_system_settings_key").IsUnique();
        builder.HasIndex(s => s.DeletedAt).HasDatabaseName("idx_system_settings_deleted_at");
        builder.HasQueryFilter(s => s.DeletedAt == null);
    }
}
