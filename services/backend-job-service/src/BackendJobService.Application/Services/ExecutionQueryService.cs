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

    public async Task<List<JobExecutionResponse>> ListExecutionsByJobAsync(long jobId, int limit, CancellationToken cancellationToken)
    {
        var executions = await executionRepository.ListByJobIdAsync(jobId, limit, cancellationToken);
        return executions.Select(JobExecutionResponse.FromEntity).ToList();
    }

    public async Task<JobStatusResponse> GetJobStatusAsync(long jobId, CancellationToken cancellationToken)
    {
        var job = await jobRepository.GetByIdAsync(jobId, cancellationToken)
            ?? throw new NotFoundException($"job {jobId} not found");
        var latestExecution = await executionRepository.GetLatestByJobIdAsync(jobId, cancellationToken);
        return JobStatusResponse.FromEntities(job, latestExecution);
    }
}
