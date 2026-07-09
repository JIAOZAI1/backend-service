namespace BackendJobService.Domain.Entities;

public enum JobExecutionStatus
{
    Pending = 1,
    Running = 2,
    Succeeded = 3,
    Failed = 4,
}

/// <summary>
/// 一次调度触发对应一条 JobExecution 记录，Job 校验通过后由 Scheduler 创建，
/// 状态随其下所有 TaskExecution 的完成情况推进。
/// </summary>
public class JobExecution
{
    public long Id { get; set; }
    public long JobId { get; set; }
    public JobExecutionStatus Status { get; set; } = JobExecutionStatus.Pending;

    public DateTime TriggeredAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }

    public string? ErrorMessage { get; set; }

    public Job? Job { get; set; }
    public List<TaskExecution> TaskExecutions { get; set; } = [];
}
