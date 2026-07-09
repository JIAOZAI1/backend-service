using BackendJobService.Application.Interfaces;
using BackendJobService.Domain.Entities;
using BackendJobService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BackendJobService.Infrastructure.Repositories;

public class JobRepository(JobDbContext db) : IJobRepository
{
    public Task<Job?> GetByIdAsync(long id, CancellationToken cancellationToken) =>
        db.Jobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

    public Task<Job?> GetWithTasksByIdAsync(long id, CancellationToken cancellationToken) =>
        db.Jobs.Include(j => j.Tasks).FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

    public Task<List<Job>> ListDueJobsAsync(DateTime asOf, CancellationToken cancellationToken) =>
        db.Jobs
            .Where(j => j.Status == JobStatus.Enabled && j.NextRunAt != null && j.NextRunAt <= asOf)
            .Include(j => j.Tasks)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Job job, CancellationToken cancellationToken) =>
        await db.Jobs.AddAsync(job, cancellationToken);

    public async Task<JobTask> AddTaskAsync(JobTask task, CancellationToken cancellationToken)
    {
        var entry = await db.JobTasks.AddAsync(task, cancellationToken);
        return entry.Entity;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);
}
