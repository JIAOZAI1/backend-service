using BackendJobService.Application.DTOs;

namespace BackendJobService.Application.Services;

public interface IExecutionQueryService
{
    Task<JobExecutionResponse> GetExecutionAsync(long executionId, CancellationToken cancellationToken);
    Task<List<JobExecutionResponse>> ListExecutionsByJobAsync(long jobId, int limit, CancellationToken cancellationToken);
    Task<JobStatusResponse> GetJobStatusAsync(long jobId, CancellationToken cancellationToken);
}
