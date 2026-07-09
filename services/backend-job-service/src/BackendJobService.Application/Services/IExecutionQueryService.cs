using BackendJobService.Application.DTOs;

namespace BackendJobService.Application.Services;

public interface IExecutionQueryService
{
    Task<JobExecutionResponse> GetExecutionAsync(long executionId, CancellationToken cancellationToken);
    Task<PagedResult<JobExecutionResponse>> ListExecutionsByJobAsync(long jobId, int page, int pageSize, CancellationToken cancellationToken);
    Task<JobStatusResponse> GetJobStatusAsync(long jobId, CancellationToken cancellationToken);
}
