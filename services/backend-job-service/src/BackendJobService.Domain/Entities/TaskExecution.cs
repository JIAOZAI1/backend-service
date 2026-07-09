namespace BackendJobService.Domain.Entities;

public enum TaskExecutionStatus
{
    Pending = 1,
    Running = 2,
    Succeeded = 3,
    Failed = 4,
    TimedOut = 5,
}

/// <summary>
/// 一个 JobExecution 下，某个 JobTask 的一次执行记录。重试会在同一条记录上递增
/// AttemptCount，而不是新建记录——保留最终结果与失败历史摘要即可满足基础版排查需求。
/// </summary>
public class TaskExecution
{
    public long Id { get; set; }
    public long JobExecutionId { get; set; }
    public long JobTaskId { get; set; }

    public TaskExecutionStatus Status { get; set; } = TaskExecutionStatus.Pending;
    public int AttemptCount { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }

    public string? OutputJson { get; set; }
    public string? ErrorMessage { get; set; }

    public JobExecution? JobExecution { get; set; }
    public JobTask? JobTask { get; set; }
}
