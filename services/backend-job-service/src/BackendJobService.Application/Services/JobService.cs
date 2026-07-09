using BackendJobService.Application.DTOs;
using BackendJobService.Application.Exceptions;
using BackendJobService.Application.Interfaces;
using BackendJobService.Application.Validators;
using BackendJobService.Domain.Entities;

namespace BackendJobService.Application.Services;

public class JobService(IJobRepository jobRepository) : IJobService
{
    public async Task<JobResponse> CreateJobAsync(CreateJobRequest request, CancellationToken cancellationToken)
    {
        JobValidator.ValidateCreateRequest(request);

        var job = new Job
        {
            Name = request.Name,
            Description = request.Description,
            ScheduleType = request.ScheduleType,
            CronExpression = request.CronExpression,
            RunAt = request.RunAt,
            Status = JobStatus.Enabled,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        job.NextRunAt = JobValidator.ComputeNextRunAt(job, DateTime.UtcNow);

        await jobRepository.AddAsync(job, cancellationToken);
        await jobRepository.SaveChangesAsync(cancellationToken);

        return JobResponse.FromEntity(job);
    }

    public async Task<JobResponse> UpdateJobAsync(long jobId, UpdateJobRequest request, CancellationToken cancellationToken)
    {
        JobValidator.ValidateUpdateRequest(request);

        var job = await jobRepository.GetByIdAsync(jobId, cancellationToken)
            ?? throw new NotFoundException($"job {jobId} not found");

        job.Name = request.Name;
        job.Description = request.Description;
        job.ScheduleType = request.ScheduleType;
        job.CronExpression = request.CronExpression;
        job.RunAt = request.RunAt;
        job.Status = request.Status;
        job.UpdatedAt = DateTime.UtcNow;
        job.NextRunAt = job.Status == JobStatus.Enabled
            ? JobValidator.ComputeNextRunAt(job, DateTime.UtcNow)
            : null;

        await jobRepository.SaveChangesAsync(cancellationToken);

        return JobResponse.FromEntity(job);
    }

    public async Task DeleteJobAsync(long jobId, CancellationToken cancellationToken)
    {
        var job = await jobRepository.GetByIdAsync(jobId, cancellationToken)
            ?? throw new NotFoundException($"job {jobId} not found");

        job.DeletedAt = DateTime.UtcNow;
        job.UpdatedAt = job.DeletedAt.Value;

        await jobRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<JobTaskResponse> CreateJobTaskAsync(long jobId, CreateJobTaskRequest request, CancellationToken cancellationToken)
    {
        var job = await jobRepository.GetByIdAsync(jobId, cancellationToken)
            ?? throw new NotFoundException($"job {jobId} not found");

        var task = new JobTask
        {
            JobId = job.Id,
            Name = request.Name,
            Order = request.Order,
            HandlerType = request.HandlerType,
            PluginAssembly = request.PluginAssembly,
            ParametersJson = request.ParametersJson,
            TimeoutSeconds = request.TimeoutSeconds,
            MaxRetryCount = request.MaxRetryCount,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var created = await jobRepository.AddTaskAsync(task, cancellationToken);
        await jobRepository.SaveChangesAsync(cancellationToken);

        return JobTaskResponse.FromEntity(created);
    }

    public async Task<JobTaskResponse> UpdateJobTaskAsync(long jobId, long taskId, UpdateJobTaskRequest request, CancellationToken cancellationToken)
    {
        var task = await jobRepository.GetTaskByIdAsync(jobId, taskId, cancellationToken)
            ?? throw new NotFoundException($"task {taskId} not found for job {jobId}");

        task.Name = request.Name;
        task.Order = request.Order;
        task.HandlerType = request.HandlerType;
        task.PluginAssembly = request.PluginAssembly;
        task.ParametersJson = request.ParametersJson;
        task.TimeoutSeconds = request.TimeoutSeconds;
        task.MaxRetryCount = request.MaxRetryCount;
        task.UpdatedAt = DateTime.UtcNow;

        await jobRepository.SaveChangesAsync(cancellationToken);

        return JobTaskResponse.FromEntity(task);
    }

    public async Task DeleteJobTaskAsync(long jobId, long taskId, CancellationToken cancellationToken)
    {
        var task = await jobRepository.GetTaskByIdAsync(jobId, taskId, cancellationToken)
            ?? throw new NotFoundException($"task {taskId} not found for job {jobId}");

        task.DeletedAt = DateTime.UtcNow;
        task.UpdatedAt = task.DeletedAt.Value;

        await jobRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<JobResponse> GetJobAsync(long jobId, CancellationToken cancellationToken)
    {
        var job = await jobRepository.GetByIdAsync(jobId, cancellationToken)
            ?? throw new NotFoundException($"job {jobId} not found");
        return JobResponse.FromEntity(job);
    }

    public async Task<List<JobTaskResponse>> ListJobTasksAsync(long jobId, CancellationToken cancellationToken)
    {
        var job = await jobRepository.GetWithTasksByIdAsync(jobId, cancellationToken)
            ?? throw new NotFoundException($"job {jobId} not found");
        return job.Tasks.OrderBy(t => t.Order).Select(JobTaskResponse.FromEntity).ToList();
    }

    public async Task<PagedResult<JobResponse>> ListJobsAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var (items, total) = await jobRepository.ListPagedAsync(page, pageSize, cancellationToken);
        return new PagedResult<JobResponse>
        {
            Items = items.Select(JobResponse.FromEntity).ToList(),
            Page = page,
            PageSize = pageSize,
            Total = total,
        };
    }
}
