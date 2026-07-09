using BackendJobService.Application.DTOs;
using BackendJobService.Application.Exceptions;
using BackendJobService.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace BackendJobService.Api.Controllers;

[ApiController]
[Route("backend-job-service/api/v1/jobs/{jobId:long}/tasks")]
public class TasksController(IJobService jobService) : ControllerBase
{
    [HttpPut("{taskId:long}")]
    public async Task<ActionResult<JobTaskResponse>> UpdateTask(long jobId, long taskId, [FromBody] UpdateJobTaskRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await jobService.UpdateJobTaskAsync(jobId, taskId, request, cancellationToken));
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpDelete("{taskId:long}")]
    public async Task<IActionResult> DeleteTask(long jobId, long taskId, CancellationToken cancellationToken)
    {
        try
        {
            await jobService.DeleteJobTaskAsync(jobId, taskId, cancellationToken);
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
