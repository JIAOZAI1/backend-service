using AdminService.Application.Common;
using AdminService.Application.DTOs;
using AdminService.Application.Interfaces;

namespace AdminService.Application.Services;

public class TenantQueryService(ITenantRepository tenantRepository) : ITenantQueryService
{
    private static readonly IReadOnlySet<string> _sortFields =
        new HashSet<string>(["id", "tenantCode", "status", "createdAt"], StringComparer.OrdinalIgnoreCase);

    public async Task<PagedResult<TenantResponse>> ListTenantsAsync(
        int page, int pageSize, string? sortBy, SortOrder sortOrder, CancellationToken cancellationToken)
    {
        var sort = SortSpec.Resolve(sortBy, sortOrder, _sortFields, defaultField: "createdAt");
        var (items, total) = await tenantRepository.ListPagedAsync(page, pageSize, sort, cancellationToken);
        return new PagedResult<TenantResponse>
        {
            Items = items.Select(TenantResponse.FromEntity).ToList(),
            Page = page,
            PageSize = pageSize,
            Total = total,
        };
    }
}
