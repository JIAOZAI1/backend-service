using AdminService.Application.Common;
using AdminService.Domain.Entities;

namespace AdminService.Application.Interfaces;

public interface IDatabaseInstanceRepository
{
    Task<DatabaseInstance?> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<bool> ExistsByNameAsync(string name, long? excludingId, CancellationToken cancellationToken);
    Task AddAsync(DatabaseInstance instance, CancellationToken cancellationToken);
    Task UpdateAsync(DatabaseInstance instance, CancellationToken cancellationToken);
    Task DeleteAsync(DatabaseInstance instance, CancellationToken cancellationToken);
    Task<(List<DatabaseInstance> Items, long Total)> ListPagedAsync(
        int page, int pageSize, SortSpec sort, CancellationToken cancellationToken);
}
