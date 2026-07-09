using BackendJobService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BackendJobService.Infrastructure.Persistence.Configurations;

public class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("jobs");

        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).HasColumnName("id");

        builder.Property(j => j.Name).HasColumnName("name").HasMaxLength(128).IsRequired();
        builder.Property(j => j.Description).HasColumnName("description").HasMaxLength(512).IsRequired();
        builder.Property(j => j.ScheduleType).HasColumnName("schedule_type").IsRequired();
        builder.Property(j => j.CronExpression).HasColumnName("cron_expression").HasMaxLength(128);
        builder.Property(j => j.RunAt).HasColumnName("run_at");
        builder.Property(j => j.Status).HasColumnName("status").IsRequired();
        builder.Property(j => j.NextRunAt).HasColumnName("next_run_at");
        builder.Property(j => j.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(j => j.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(j => j.DeletedAt).HasColumnName("deleted_at");

        builder.HasIndex(j => j.NextRunAt).HasDatabaseName("idx_jobs_next_run_at");
        builder.HasIndex(j => j.DeletedAt).HasDatabaseName("idx_jobs_deleted_at");
        builder.HasQueryFilter(j => j.DeletedAt == null);

        builder.HasMany(j => j.Tasks)
            .WithOne(t => t.Job)
            .HasForeignKey(t => t.JobId);
    }
}
