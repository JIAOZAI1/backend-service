using AdminService.Application.Common;
using AdminService.Application.DTOs;
using AdminService.Application.Exceptions;
using AdminService.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AdminService.Api.Controllers;

[ApiController]
[Route("admin-service/api/v1/tenants")]
public class TenantsController(ITenantQueryService tenantQueryService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<TenantResponse>>> ListTenants(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromQuery] string? sortBy,
        [FromQuery] SortOrder sortOrder,
        CancellationToken cancellationToken)
    {
        var effectivePage = page > 0 ? page : 1;
        var effectivePageSize = pageSize is > 0 and <= 200 ? pageSize : 20;
        try
        {
            return Ok(await tenantQueryService.ListTenantsAsync(effectivePage, effectivePageSize, sortBy, sortOrder, cancellationToken));
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
