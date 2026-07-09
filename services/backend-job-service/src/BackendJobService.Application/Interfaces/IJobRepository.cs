using BackendJobService.Domain.Entities;

namespace BackendJobService.Application.Interfaces;

public interface IJobRepository
{
    Task<Job?> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<Job?> GetWithTasksByIdAsync(long id, CancellationToken cancellationToken);
    Task<List<Job>> ListDueJobsAsync(DateTime asOf, CancellationToken cancellationToken);
    Task<(List<Job> Items, long Total)> ListPagedAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task AddAsync(Job job, CancellationToken cancellationToken);
    Task<JobTask?> GetTaskByIdAsync(long jobId, long taskId, CancellationToken cancellationToken);
    Task<JobTask> AddTaskAsync(JobTask task, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
