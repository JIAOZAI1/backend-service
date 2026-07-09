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
}
