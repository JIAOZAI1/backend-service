using AdminService.Application.Common;
using AdminService.Domain.Entities;

namespace AdminService.Application.Interfaces;

public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<(List<Tenant> Items, long Total)> ListPagedAsync(int page, int pageSize, SortSpec sort, CancellationToken cancellationToken);
    Task AddAsync(Tenant tenant, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
