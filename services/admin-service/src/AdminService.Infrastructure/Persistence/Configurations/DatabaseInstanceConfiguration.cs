using AdminService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdminService.Infrastructure.Persistence.Configurations;

public class DatabaseInstanceConfiguration : IEntityTypeConfiguration<DatabaseInstance>
{
    public void Configure(EntityTypeBuilder<DatabaseInstance> builder)
    {
        builder.ToTable("database_instances");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id");

        builder.Property(d => d.Name).HasColumnName("name").HasMaxLength(128).IsRequired();
        builder.Property(d => d.DbType).HasColumnName("db_type")
            .HasConversion(
                type => type.ToString().ToLowerInvariant(),
                value => Enum.Parse<DatabaseInstanceType>(value, ignoreCase: true))
            .HasMaxLength(32).IsRequired();
        builder.Property(d => d.Host).HasColumnName("host").HasMaxLength(255).IsRequired();
        builder.Property(d => d.Port).HasColumnName("port").IsRequired();
        builder.Property(d => d.Username).HasColumnName("username").HasMaxLength(64).IsRequired();
        builder.Property(d => d.EncryptedPassword).HasColumnName("encrypted_password").HasMaxLength(512).IsRequired();
        builder.Property(d => d.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(d => d.DeletedAt).HasColumnName("deleted_at");

        builder.HasIndex(d => d.Name).HasDatabaseName("uk_database_instances_name").IsUnique();
        builder.HasIndex(d => d.DeletedAt).HasDatabaseName("idx_database_instances_deleted_at");
        builder.HasQueryFilter(d => d.DeletedAt == null);
    }
}
