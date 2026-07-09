using BackendJobService.Application.DTOs;

namespace BackendJobService.Application.Services;

public interface IJobService
{
    Task<JobResponse> CreateJobAsync(CreateJobRequest request, CancellationToken cancellationToken);
    Task<JobTaskResponse> CreateJobTaskAsync(long jobId, CreateJobTaskRequest request, CancellationToken cancellationToken);
    Task<JobResponse> GetJobAsync(long jobId, CancellationToken cancellationToken);
    Task<List<JobTaskResponse>> ListJobTasksAsync(long jobId, CancellationToken cancellationToken);
    Task<PagedResult<JobResponse>> ListJobsAsync(int page, int pageSize, CancellationToken cancellationToken);
}
