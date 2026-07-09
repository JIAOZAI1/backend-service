using BackendJobService.Application.Exceptions;
using BackendJobService.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace BackendJobService.Api.Controllers;

[ApiController]
[Route("backend-job-service/api/v1/executions")]
public class ExecutionsController(IExecutionQueryService executionQueryService) : ControllerBase
{
    [HttpGet("{executionId:long}")]
    public async Task<IActionResult> GetExecution(long executionId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await executionQueryService.GetExecutionAsync(executionId, cancellationToken));
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
