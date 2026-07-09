using BackendJobService.Domain.Entities;

namespace BackendJobService.Application.Interfaces;

public interface IJobExecutionRepository
{
    Task<JobExecution?> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<JobExecution?> GetWithTaskExecutionsByIdAsync(long id, CancellationToken cancellationToken);
    Task<(List<JobExecution> Items, long Total)> ListPagedByJobIdAsync(long jobId, int page, int pageSize, CancellationToken cancellationToken);
    Task<JobExecution?> GetLatestByJobIdAsync(long jobId, CancellationToken cancellationToken);
    Task AddAsync(JobExecution execution, CancellationToken cancellationToken);
    Task AddTaskExecutionAsync(TaskExecution taskExecution, CancellationToken cancellationToken);
    Task<TaskExecution?> GetTaskExecutionByIdAsync(long id, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
