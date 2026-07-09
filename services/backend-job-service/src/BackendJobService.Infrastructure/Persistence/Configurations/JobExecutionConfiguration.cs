using BackendJobService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BackendJobService.Infrastructure.Persistence.Configurations;

public class JobExecutionConfiguration : IEntityTypeConfiguration<JobExecution>
{
    public void Configure(EntityTypeBuilder<JobExecution> builder)
    {
        builder.ToTable("job_executions");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.JobId).HasColumnName("job_id").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").IsRequired();
        builder.Property(e => e.TriggeredAt).HasColumnName("triggered_at").IsRequired();
        builder.Property(e => e.StartedAt).HasColumnName("started_at");
        builder.Property(e => e.FinishedAt).HasColumnName("finished_at");
        builder.Property(e => e.ErrorMessage).HasColumnName("error_message").HasColumnType("text");

        builder.HasIndex(e => e.JobId).HasDatabaseName("idx_job_executions_job_id");
        builder.HasIndex(e => e.Status).HasDatabaseName("idx_job_executions_status");

        // Job 上有软删除的全局查询过滤器；导航按可选关系配置，避免 Job 被软删除后
        // 因过滤器联动导致历史 JobExecution 记录“查不到关联 Job”而被意外过滤掉。
        builder.HasOne(e => e.Job)
            .WithMany()
            .HasForeignKey(e => e.JobId)
            .IsRequired(false);

        builder.HasMany(e => e.TaskExecutions)
            .WithOne(t => t.JobExecution)
            .HasForeignKey(t => t.JobExecutionId);
    }
}
