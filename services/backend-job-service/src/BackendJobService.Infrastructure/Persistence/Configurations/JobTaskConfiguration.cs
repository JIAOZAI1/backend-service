using BackendJobService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BackendJobService.Infrastructure.Persistence.Configurations;

public class JobTaskConfiguration : IEntityTypeConfiguration<JobTask>
{
    public void Configure(EntityTypeBuilder<JobTask> builder)
    {
        builder.ToTable("job_tasks");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");

        builder.Property(t => t.JobId).HasColumnName("job_id").IsRequired();
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(128).IsRequired();
        builder.Property(t => t.Order).HasColumnName("order").IsRequired();
        builder.Property(t => t.HandlerType).HasColumnName("handler_type").HasMaxLength(256).IsRequired();
        builder.Property(t => t.PluginAssembly).HasColumnName("plugin_assembly").HasMaxLength(256).IsRequired();
        builder.Property(t => t.ParametersJson).HasColumnName("parameters_json").HasColumnType("json").IsRequired();
        builder.Property(t => t.TimeoutSeconds).HasColumnName("timeout_seconds").IsRequired();
        builder.Property(t => t.MaxRetryCount).HasColumnName("max_retry_count").IsRequired();
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(t => t.DeletedAt).HasColumnName("deleted_at");

        builder.HasIndex(t => new { t.JobId, t.Order }).HasDatabaseName("uk_job_tasks_job_order").IsUnique();
        builder.HasIndex(t => t.DeletedAt).HasDatabaseName("idx_job_tasks_deleted_at");
        builder.HasQueryFilter(t => t.DeletedAt == null);
    }
}
