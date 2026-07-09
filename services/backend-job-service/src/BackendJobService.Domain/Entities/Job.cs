namespace BackendJobService.Domain.Entities;

public enum JobScheduleType
{
    Cron = 1,
    OneTime = 2,
}

public enum JobStatus
{
    Enabled = 1,
    Disabled = 2,
}

public class Job
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public JobScheduleType ScheduleType { get; set; }

    /// <summary>ScheduleType=Cron 时的 Cron 表达式；OneTime 时为空。</summary>
    public string? CronExpression { get; set; }

    /// <summary>ScheduleType=OneTime 时的执行时间点；Cron 时为空。</summary>
    public DateTime? RunAt { get; set; }

    public JobStatus Status { get; set; } = JobStatus.Enabled;

    /// <summary>下一次应该被 Scheduler 扫描到并触发的时间，由 Scheduler 每次触发后重新计算。</summary>
    public DateTime? NextRunAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public List<JobTask> Tasks { get; set; } = [];
}
