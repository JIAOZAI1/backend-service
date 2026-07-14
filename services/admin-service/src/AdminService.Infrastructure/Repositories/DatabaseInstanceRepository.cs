using AdminService.Application.Common;
using AdminService.Application.Interfaces;
using AdminService.Domain.Entities;
using AdminService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdminService.Infrastructure.Repositories;

public class DatabaseInstanceRepository(AdminDbContext dbContext) : IDatabaseInstanceRepository
{
    private static readonly Dictionary<string, Func<IQueryable<DatabaseInstance>, SortOrder, IOrderedQueryable<DatabaseInstance>>> _sorters = new(StringComparer.OrdinalIgnoreCase)
    {
        ["id"] = (q, o) => o == SortOrder.Asc ? q.OrderBy(d => d.Id) : q.OrderByDescending(d => d.Id),
        ["name"] = (q, o) => o == SortOrder.Asc ? q.OrderBy(d => d.Name) : q.OrderByDescending(d => d.Name),
        ["dbType"] = (q, o) => o == SortOrder.Asc ? q.OrderBy(d => d.DbType) : q.OrderByDescending(d => d.DbType),
        ["createdAt"] = (q, o) => o == SortOrder.Asc ? q.OrderBy(d => d.CreatedAt) : q.OrderByDescending(d => d.CreatedAt),
        ["updatedAt"] = (q, o) => o == SortOrder.Asc ? q.OrderBy(d => d.UpdatedAt) : q.OrderByDescending(d => d.UpdatedAt),
    };

    public Task<DatabaseInstance?> GetByIdAsync(long id, CancellationToken cancellationToken) =>
        dbContext.DatabaseInstances.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<bool> ExistsByNameAsync(string name, long? excludingId, CancellationToken cancellationToken) =>
        dbContext.DatabaseInstances.AnyAsync(
            d => d.Name == name && (excludingId == null || d.Id != excludingId), cancellationToken);

    public async Task AddAsync(DatabaseInstance instance, CancellationToken cancellationToken)
    {
        dbContext.DatabaseInstances.Add(instance);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(DatabaseInstance instance, CancellationToken cancellationToken)
    {
        dbContext.DatabaseInstances.Update(instance);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(DatabaseInstance instance, CancellationToken cancellationToken)
    {
        instance.DeletedAt = DateTime.UtcNow;
        dbContext.DatabaseInstances.Update(instance);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<(List<DatabaseInstance> Items, long Total)> ListPagedAsync(
        int page, int pageSize, SortSpec sort, CancellationToken cancellationToken)
    {
        if (!_sorters.TryGetValue(sort.SortBy, out var sorter))
        {
            throw new InvalidOperationException($"no sorter registered for field: {sort.SortBy}");
        }

        var query = sorter(dbContext.DatabaseInstances.AsNoTracking(), sort.SortOrder);
        var total = await query.LongCountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }
}
