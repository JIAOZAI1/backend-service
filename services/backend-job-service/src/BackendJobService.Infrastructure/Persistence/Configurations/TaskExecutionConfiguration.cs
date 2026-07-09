using BackendJobService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BackendJobService.Infrastructure.Persistence.Configurations;

public class TaskExecutionConfiguration : IEntityTypeConfiguration<TaskExecution>
{
    public void Configure(EntityTypeBuilder<TaskExecution> builder)
    {
        builder.ToTable("task_executions");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");

        builder.Property(t => t.JobExecutionId).HasColumnName("job_execution_id").IsRequired();
        builder.Property(t => t.JobTaskId).HasColumnName("job_task_id").IsRequired();
        builder.Property(t => t.Status).HasColumnName("status").IsRequired();
        builder.Property(t => t.AttemptCount).HasColumnName("attempt_count").IsRequired();
        builder.Property(t => t.StartedAt).HasColumnName("started_at");
        builder.Property(t => t.FinishedAt).HasColumnName("finished_at");
        builder.Property(t => t.OutputJson).HasColumnName("output_json").HasColumnType("json");
        builder.Property(t => t.ErrorMessage).HasColumnName("error_message").HasColumnType("text");

        builder.HasIndex(t => t.JobExecutionId).HasDatabaseName("idx_task_executions_job_execution_id");
        builder.HasIndex(t => t.JobTaskId).HasDatabaseName("idx_task_executions_job_task_id");

        // JobTask 上有软删除的全局查询过滤器；导航按可选关系配置，理由同 JobExecutionConfiguration
        // 里对 Job 导航的处理——避免软删除后历史 TaskExecution 记录被过滤器联动误伤。
        builder.HasOne(t => t.JobTask)
            .WithMany()
            .HasForeignKey(t => t.JobTaskId)
            .IsRequired(false);
    }
}
