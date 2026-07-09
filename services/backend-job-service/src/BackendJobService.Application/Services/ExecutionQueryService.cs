using BackendJobService.Application.DTOs;
using BackendJobService.Application.Exceptions;
using BackendJobService.Application.Interfaces;

namespace BackendJobService.Application.Services;

public class ExecutionQueryService(IJobExecutionRepository executionRepository) : IExecutionQueryService
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
}
