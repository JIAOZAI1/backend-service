using AdminService.Application.Common;
using AdminService.Application.DTOs;

namespace AdminService.Application.Interfaces;

public interface IDatabaseInstanceService
{
    Task<PagedResult<DatabaseInstanceResponse>> ListInstancesAsync(
        int page, int pageSize, string? sortBy, SortOrder sortOrder, CancellationToken cancellationToken);
    Task<DatabaseInstanceResponse> GetInstanceAsync(long id, CancellationToken cancellationToken);
    Task<DatabaseInstanceResponse> CreateInstanceAsync(CreateDatabaseInstanceRequest request, CancellationToken cancellationToken);
    Task<DatabaseInstanceResponse> UpdateInstanceAsync(long id, UpdateDatabaseInstanceRequest request, CancellationToken cancellationToken);
    Task DeleteInstanceAsync(long id, CancellationToken cancellationToken);
}
