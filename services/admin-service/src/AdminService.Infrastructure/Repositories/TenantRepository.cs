using AdminService.Application.Common;
using AdminService.Application.Interfaces;
using AdminService.Domain.Entities;
using AdminService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdminService.Infrastructure.Repositories;

public class TenantRepository(AdminDbContext dbContext) : ITenantRepository
{
    private static readonly Dictionary<string, Func<IQueryable<Tenant>, SortOrder, IOrderedQueryable<Tenant>>> _sorters = new(StringComparer.OrdinalIgnoreCase)
    {
        ["id"] = (q, o) => o == SortOrder.Asc ? q.OrderBy(t => t.Id) : q.OrderByDescending(t => t.Id),
        ["tenantCode"] = (q, o) => o == SortOrder.Asc ? q.OrderBy(t => t.TenantCode) : q.OrderByDescending(t => t.TenantCode),
        ["status"] = (q, o) => o == SortOrder.Asc ? q.OrderBy(t => t.Status) : q.OrderByDescending(t => t.Status),
        ["createdAt"] = (q, o) => o == SortOrder.Asc ? q.OrderBy(t => t.CreatedAt) : q.OrderByDescending(t => t.CreatedAt),
    };

    public Task<Tenant?> GetByIdAsync(long id, CancellationToken cancellationToken) =>
        dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<Tenant?> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken) =>
        dbContext.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId, cancellationToken);

    public async Task<(List<Tenant> Items, long Total)> ListPagedAsync(int page, int pageSize, SortSpec sort, CancellationToken cancellationToken)
    {
        if (!_sorters.TryGetValue(sort.SortBy, out var sorter))
        {
            throw new InvalidOperationException($"no sorter registered for field: {sort.SortBy}");
        }

        var query = sorter(dbContext.Tenants.AsNoTracking(), sort.SortOrder);
        var total = await query.LongCountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task AddAsync(Tenant tenant, CancellationToken cancellationToken) =>
        await dbContext.Tenants.AddAsync(tenant, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
