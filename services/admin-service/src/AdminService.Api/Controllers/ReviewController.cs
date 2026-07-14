using AdminService.Api.Auth;
using AdminService.Application.DTOs;
using AdminService.Application.Exceptions;
using AdminService.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AdminService.Api.Controllers;

[ApiController]
[Route("admin-service/api/v1/reviews")]
public class ReviewController(IReviewService reviewService) : ControllerBase
{
    [HttpPost("{userId}/approve")]
    public async Task<ActionResult<ApproveReviewResponse>> Approve(ulong userId, CancellationToken cancellationToken)
    {
        var admin = (GatewayUser)HttpContext.Items[nameof(GatewayUser)]!;

        try
        {
            return Ok(await reviewService.ApproveAsync(userId, admin.UserId, cancellationToken));
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ReviewStepFailedException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { failedStep = ex.Step, message = ex.Message });
        }
    }
}
