using AdminService.Application.Common;
using AdminService.Application.DTOs;
using AdminService.Application.Exceptions;
using AdminService.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AdminService.Api.Controllers;

[ApiController]
[Route("admin-service/api/v1/database-instances")]
public class DatabaseInstancesController(IDatabaseInstanceService databaseInstanceService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<DatabaseInstanceResponse>>> ListInstances(
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
            return Ok(await databaseInstanceService.ListInstancesAsync(
                effectivePage, effectivePageSize, sortBy, sortOrder, cancellationToken));
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DatabaseInstanceResponse>> GetInstance(long id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await databaseInstanceService.GetInstanceAsync(id, cancellationToken));
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<DatabaseInstanceResponse>> CreateInstance(
        [FromBody] CreateDatabaseInstanceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await databaseInstanceService.CreateInstanceAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetInstance), new { id = created.Id }, created);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<DatabaseInstanceResponse>> UpdateInstance(
        long id, [FromBody] UpdateDatabaseInstanceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await databaseInstanceService.UpdateInstanceAsync(id, request, cancellationToken));
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteInstance(long id, CancellationToken cancellationToken)
    {
        try
        {
            await databaseInstanceService.DeleteInstanceAsync(id, cancellationToken);
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
