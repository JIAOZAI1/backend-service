using BackendJobService.Application.Common;
using BackendJobService.Application.DTOs;

namespace BackendJobService.Application.Services;

public interface IJobService
{
    Task<JobResponse> CreateJobAsync(CreateJobRequest request, CancellationToken cancellationToken);
    Task<JobResponse> UpdateJobAsync(long jobId, UpdateJobRequest request, CancellationToken cancellationToken);
    Task DeleteJobAsync(long jobId, CancellationToken cancellationToken);
    Task<JobTaskResponse> CreateJobTaskAsync(long jobId, CreateJobTaskRequest request, CancellationToken cancellationToken);
    Task<JobTaskResponse> UpdateJobTaskAsync(long jobId, long taskId, UpdateJobTaskRequest request, CancellationToken cancellationToken);
    Task DeleteJobTaskAsync(long jobId, long taskId, CancellationToken cancellationToken);
    Task<JobResponse> GetJobAsync(long jobId, CancellationToken cancellationToken);
    Task<PagedResult<JobTaskResponse>> ListJobTasksAsync(long jobId, int page, int pageSize, string? sortBy, SortOrder sortOrder, CancellationToken cancellationToken);
    Task<PagedResult<JobResponse>> ListJobsAsync(int page, int pageSize, string? sortBy, SortOrder sortOrder, CancellationToken cancellationToken);
}
