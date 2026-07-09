using BackendJobService.Domain.Entities;

namespace BackendJobService.Application.DTOs;

public class JobExecutionResponse
{
    public long Id { get; init; }
    public long JobId { get; init; }
    public JobExecutionStatus Status { get; init; }
    public DateTime TriggeredAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? FinishedAt { get; init; }
    public string? ErrorMessage { get; init; }
    public List<TaskExecutionResponse> TaskExecutions { get; init; } = [];

    public static JobExecutionResponse FromEntity(JobExecution execution) => new()
    {
        Id = execution.Id,
        JobId = execution.JobId,
        Status = execution.Status,
        TriggeredAt = execution.TriggeredAt,
        StartedAt = execution.StartedAt,
        FinishedAt = execution.FinishedAt,
        ErrorMessage = execution.ErrorMessage,
        TaskExecutions = execution.TaskExecutions.Select(TaskExecutionResponse.FromEntity).ToList(),
    };
}

/// <summary>
/// 供前端轮询的作业状态聚合视图：作业本身状态 + 最近一次执行（含各 Task 状态），一次请求拿全。
/// </summary>
public class JobStatusResponse
{
    public long JobId { get; init; }
    public JobStatus JobStatus { get; init; }
    public DateTime? NextRunAt { get; init; }
    public JobExecutionResponse? LatestExecution { get; init; }

    public static JobStatusResponse FromEntities(Job job, JobExecution? latestExecution) => new()
    {
        JobId = job.Id,
        JobStatus = job.Status,
        NextRunAt = job.NextRunAt,
        LatestExecution = latestExecution is null ? null : JobExecutionResponse.FromEntity(latestExecution),
    };
}

public class TaskExecutionResponse
{
    public long Id { get; init; }
    public long JobTaskId { get; init; }
    public TaskExecutionStatus Status { get; init; }
    public int AttemptCount { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? FinishedAt { get; init; }
    public string? OutputJson { get; init; }
    public string? ErrorMessage { get; init; }

    public static TaskExecutionResponse FromEntity(TaskExecution te) => new()
    {
        Id = te.Id,
        JobTaskId = te.JobTaskId,
        Status = te.Status,
        AttemptCount = te.AttemptCount,
        StartedAt = te.StartedAt,
        FinishedAt = te.FinishedAt,
        OutputJson = te.OutputJson,
        ErrorMessage = te.ErrorMessage,
    };
}
