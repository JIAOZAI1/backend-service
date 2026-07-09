using BackendJobService.Application.Interfaces;
using BackendJobService.Domain.Entities;
using BackendJobService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BackendJobService.Infrastructure.Repositories;

public class JobRepository(JobDbContext db) : IJobRepository
{
    public Task<Job?> GetByIdAsync(long id, CancellationToken cancellationToken) =>
        db.Jobs.FirstOrDefaultAsync(j => j.Id == id && j.DeletedAt == null, cancellationToken);

    public Task<Job?> GetWithTasksByIdAsync(long id, CancellationToken cancellationToken) =>
        db.Jobs
            .Include(j => j.Tasks.Where(t => t.DeletedAt == null))
            .FirstOrDefaultAsync(j => j.Id == id && j.DeletedAt == null, cancellationToken);

    public Task<List<Job>> ListDueJobsAsync(DateTime asOf, CancellationToken cancellationToken) =>
        db.Jobs
            .Where(j => j.DeletedAt == null && j.Status == JobStatus.Enabled && j.NextRunAt != null && j.NextRunAt <= asOf)
            .Include(j => j.Tasks.Where(t => t.DeletedAt == null))
            .ToListAsync(cancellationToken);

    public async Task<(List<Job> Items, long Total)> ListPagedAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Jobs.Where(j => j.DeletedAt == null).OrderByDescending(j => j.Id);
        var total = await query.LongCountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task AddAsync(Job job, CancellationToken cancellationToken) =>
        await db.Jobs.AddAsync(job, cancellationToken);

    public Task<JobTask?> GetTaskByIdAsync(long jobId, long taskId, CancellationToken cancellationToken) =>
        db.JobTasks.FirstOrDefaultAsync(t => t.Id == taskId && t.JobId == jobId && t.DeletedAt == null, cancellationToken);

    public async Task<JobTask> AddTaskAsync(JobTask task, CancellationToken cancellationToken)
    {
        var entry = await db.JobTasks.AddAsync(task, cancellationToken);
        return entry.Entity;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);
}
