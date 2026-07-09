using BackendJobService.Application.Interfaces;
using BackendJobService.Domain.Entities;
using BackendJobService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BackendJobService.Infrastructure.Repositories;

public class JobExecutionRepository(JobDbContext db) : IJobExecutionRepository
{
    public Task<JobExecution?> GetByIdAsync(long id, CancellationToken cancellationToken) =>
        db.JobExecutions.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public Task<JobExecution?> GetWithTaskExecutionsByIdAsync(long id, CancellationToken cancellationToken) =>
        db.JobExecutions
            .Include(e => e.TaskExecutions)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<(List<JobExecution> Items, long Total)> ListPagedByJobIdAsync(long jobId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.JobExecutions.Where(e => e.JobId == jobId);
        var total = await query.LongCountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(e => e.TriggeredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(e => e.TaskExecutions)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<JobExecution?> GetLatestByJobIdAsync(long jobId, CancellationToken cancellationToken) =>
        db.JobExecutions
            .Where(e => e.JobId == jobId)
            .OrderByDescending(e => e.TriggeredAt)
            .Include(e => e.TaskExecutions)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(JobExecution execution, CancellationToken cancellationToken) =>
        await db.JobExecutions.AddAsync(execution, cancellationToken);

    public async Task AddTaskExecutionAsync(TaskExecution taskExecution, CancellationToken cancellationToken) =>
        await db.TaskExecutions.AddAsync(taskExecution, cancellationToken);

    public Task<TaskExecution?> GetTaskExecutionByIdAsync(long id, CancellationToken cancellationToken) =>
        db.TaskExecutions.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);
}
