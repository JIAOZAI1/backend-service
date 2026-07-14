using AdminService.Application.Common;
using AdminService.Application.DTOs;

namespace AdminService.Application.Interfaces;

public interface ITenantQueryService
{
    Task<PagedResult<TenantResponse>> ListTenantsAsync(
        int page, int pageSize, string? sortBy, SortOrder sortOrder, CancellationToken cancellationToken);
}
