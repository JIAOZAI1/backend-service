using BackendJobService.Domain.Entities;

namespace BackendJobService.Application.DTOs;

public class CreateJobRequest
{
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public required JobScheduleType ScheduleType { get; init; }
    public string? CronExpression { get; init; }
    public DateTime? RunAt { get; init; }
}

public class CreateJobTaskRequest
{
    public required string Name { get; init; }
    public required int Order { get; init; }
    public required string HandlerType { get; init; }
    public required string PluginAssembly { get; init; }
    public string ParametersJson { get; init; } = "{}";
    public int TimeoutSeconds { get; init; } = 300;
    public int MaxRetryCount { get; init; }
}

public class JobResponse
{
    public long Id { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public JobScheduleType ScheduleType { get; init; }
    public string? CronExpression { get; init; }
    public DateTime? RunAt { get; init; }
    public JobStatus Status { get; init; }
    public DateTime? NextRunAt { get; init; }
    public DateTime CreatedAt { get; init; }

    public static JobResponse FromEntity(Job job) => new()
    {
        Id = job.Id,
        Name = job.Name,
        Description = job.Description,
        ScheduleType = job.ScheduleType,
        CronExpression = job.CronExpression,
        RunAt = job.RunAt,
        Status = job.Status,
        NextRunAt = job.NextRunAt,
        CreatedAt = job.CreatedAt,
    };
}

public class PagedResult<T>
{
    public required List<T> Items { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public long Total { get; init; }
}

public class JobTaskResponse
{
    public long Id { get; init; }
    public long JobId { get; init; }
    public required string Name { get; init; }
    public int Order { get; init; }
    public required string HandlerType { get; init; }
    public required string PluginAssembly { get; init; }
    public int TimeoutSeconds { get; init; }
    public int MaxRetryCount { get; init; }

    public static JobTaskResponse FromEntity(JobTask task) => new()
    {
        Id = task.Id,
        JobId = task.JobId,
        Name = task.Name,
        Order = task.Order,
        HandlerType = task.HandlerType,
        PluginAssembly = task.PluginAssembly,
        TimeoutSeconds = task.TimeoutSeconds,
        MaxRetryCount = task.MaxRetryCount,
    };
}
