using BackendJobService.Application.DTOs;
using BackendJobService.Application.Exceptions;
using BackendJobService.Application.Interfaces;

namespace BackendJobService.Application.Services;

public class ExecutionQueryService(IJobExecutionRepository executionRepository, IJobRepository jobRepository) : IExecutionQueryService
{
    public async Task<JobExecutionResponse> GetExecutionAsync(long executionId, CancellationToken cancellationToken)
    {
        var execution = await executionRepository.GetWithTaskExecutionsByIdAsync(executionId, cancellationToken)
            ?? throw new NotFoundException($"job execution {executionId} not found");
        return JobExecutionResponse.FromEntity(execution);
    }

    public async Task<PagedResult<JobExecutionResponse>> ListExecutionsByJobAsync(long jobId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var (items, total) = await executionRepository.ListPagedByJobIdAsync(jobId, page, pageSize, cancellationToken);
        return new PagedResult<JobExecutionResponse>
        {
            Items = items.Select(JobExecutionResponse.FromEntity).ToList(),
            Page = page,
            PageSize = pageSize,
            Total = total,
        };
    }

    public async Task<JobStatusResponse> GetJobStatusAsync(long jobId, CancellationToken cancellationToken)
    {
        var job = await jobRepository.GetByIdAsync(jobId, cancellationToken)
            ?? throw new NotFoundException($"job {jobId} not found");
        var latestExecution = await executionRepository.GetLatestByJobIdAsync(jobId, cancellationToken);
        return JobStatusResponse.FromEntities(job, latestExecution);
    }
}
