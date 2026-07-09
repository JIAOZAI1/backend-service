using BackendJobService.Application.Common;
using BackendJobService.Application.Interfaces;
using BackendJobService.Domain.Entities;
using BackendJobService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BackendJobService.Infrastructure.Repositories;

public class JobRepository(JobDbContext db) : IJobRepository
{
    private static readonly Dictionary<string, Func<IQueryable<Job>, SortOrder, IOrderedQueryable<Job>>> _jobSorters = new(StringComparer.OrdinalIgnoreCase)
    {
        ["id"] = (q, o) => o == SortOrder.Asc ? q.OrderBy(j => j.Id) : q.OrderByDescending(j => j.Id),
        ["name"] = (q, o) => o == SortOrder.Asc ? q.OrderBy(j => j.Name) : q.OrderByDescending(j => j.Name),
        ["status"] = (q, o) => o == SortOrder.Asc ? q.OrderBy(j => j.Status) : q.OrderByDescending(j => j.Status),
        ["createdAt"] = (q, o) => o == SortOrder.Asc ? q.OrderBy(j => j.CreatedAt) : q.OrderByDescending(j => j.CreatedAt),
        ["updatedAt"] = (q, o) => o == SortOrder.Asc ? q.OrderBy(j => j.UpdatedAt) : q.OrderByDescending(j => j.UpdatedAt),
        ["nextRunAt"] = (q, o) => o == SortOrder.Asc ? q.OrderBy(j => j.NextRunAt) : q.OrderByDescending(j => j.NextRunAt),
    };

    private static readonly Dictionary<string, Func<IQueryable<JobTask>, SortOrder, IOrderedQueryable<JobTask>>> _jobTaskSorters = new(StringComparer.OrdinalIgnoreCase)
    {
        ["id"] = (q, o) => o == SortOrder.Asc ? q.OrderBy(t => t.Id) : q.OrderByDescending(t => t.Id),
        ["name"] = (q, o) => o == SortOrder.Asc ? q.OrderBy(t => t.Name) : q.OrderByDescending(t => t.Name),
        ["order"] = (q, o) => o == SortOrder.Asc ? q.OrderBy(t => t.Order) : q.OrderByDescending(t => t.Order),
        ["createdAt"] = (q, o) => o == SortOrder.Asc ? q.OrderBy(t => t.CreatedAt) : q.OrderByDescending(t => t.CreatedAt),
        ["updatedAt"] = (q, o) => o == SortOrder.Asc ? q.OrderBy(t => t.UpdatedAt) : q.OrderByDescending(t => t.UpdatedAt),
    };

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

    public async Task<(List<Job> Items, long Total)> ListPagedAsync(int page, int pageSize, SortSpec sort, CancellationToken cancellationToken)
    {
        var query = ApplySort(db.Jobs.Where(j => j.DeletedAt == null), _jobSorters, sort);
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

    public async Task<(List<JobTask> Items, long Total)> ListTasksPagedAsync(long jobId, int page, int pageSize, SortSpec sort, CancellationToken cancellationToken)
    {
        var query = ApplySort(db.JobTasks.Where(t => t.JobId == jobId && t.DeletedAt == null), _jobTaskSorters, sort);
        var total = await query.LongCountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<JobTask> AddTaskAsync(JobTask task, CancellationToken cancellationToken)
    {
        var entry = await db.JobTasks.AddAsync(task, cancellationToken);
        return entry.Entity;
    }

    private static IOrderedQueryable<T> ApplySort<T>(
        IQueryable<T> query,
        IReadOnlyDictionary<string, Func<IQueryable<T>, SortOrder, IOrderedQueryable<T>>> sorters,
        SortSpec sort)
    {
        if (!sorters.TryGetValue(sort.SortBy, out var sorter))
        {
            throw new InvalidOperationException($"no sorter registered for field: {sort.SortBy}");
        }
        return sorter(query, sort.SortOrder);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);
}
