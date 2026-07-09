using BackendJobService.Application.DTOs;
using BackendJobService.Application.Exceptions;
using BackendJobService.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace BackendJobService.Api.Controllers;

[ApiController]
[Route("backend-job-service/api/v1/jobs")]
public class JobsController(IJobService jobService, IExecutionQueryService executionQueryService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<JobResponse>> CreateJob([FromBody] CreateJobRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var job = await jobService.CreateJobAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetJob), new { jobId = job.Id }, job);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{jobId:long}")]
    public async Task<ActionResult<JobResponse>> GetJob(long jobId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await jobService.GetJobAsync(jobId, cancellationToken));
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("{jobId:long}/tasks")]
    public async Task<ActionResult<JobTaskResponse>> CreateTask(long jobId, [FromBody] CreateJobTaskRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var task = await jobService.CreateJobTaskAsync(jobId, request, cancellationToken);
            return CreatedAtAction(nameof(ListTasks), new { jobId }, task);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("{jobId:long}/tasks")]
    public async Task<ActionResult<List<JobTaskResponse>>> ListTasks(long jobId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await jobService.ListJobTasksAsync(jobId, cancellationToken));
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("{jobId:long}/executions")]
    public async Task<ActionResult<List<JobExecutionResponse>>> ListExecutions(long jobId, [FromQuery] int limit, CancellationToken cancellationToken)
    {
        var effectiveLimit = limit is > 0 and <= 200 ? limit : 20;
        return Ok(await executionQueryService.ListExecutionsByJobAsync(jobId, effectiveLimit, cancellationToken));
    }

    [HttpGet("{jobId:long}/status")]
    public async Task<ActionResult<JobStatusResponse>> GetJobStatus(long jobId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await executionQueryService.GetJobStatusAsync(jobId, cancellationToken));
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
