using AdminService.Api.Auth;
using AdminService.Application.Common;
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
    public async Task<ActionResult<ApproveReviewResponse>> Approve(
        ulong userId, [FromBody] ApproveReviewRequest request, CancellationToken cancellationToken)
    {
        var admin = (GatewayUser)HttpContext.Items[nameof(GatewayUser)]!;

        try
        {
            return Ok(await reviewService.ApproveAsync(userId, request.DatabaseInstanceId, admin.UserId, cancellationToken));
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

    /// <summary>
    /// 拒绝审核。sso-service 侧会软删除该用户，拒绝不可撤销——拒绝后无法再对同一 userId
    /// 调用 approve/reject（统一返回 404），用户可用同一 username/email 重新注册。
    /// </summary>
    [HttpPost("{userId}/reject")]
    public async Task<IActionResult> Reject(ulong userId, CancellationToken cancellationToken)
    {
        var admin = (GatewayUser)HttpContext.Items[nameof(GatewayUser)]!;

        try
        {
            await reviewService.RejectAsync(userId, admin.UserId, cancellationToken);
            return NoContent();
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

    /// <summary>
    /// 分页查询指定审核状态的用户，供开户向导展示待审核列表。reviewStatus 缺省时
    /// sso-service 默认取 pending，也可传 approved/rejected 查其他状态。
    /// </summary>
    [HttpGet("users")]
    public async Task<ActionResult<PagedResult<SsoUserInfo>>> ListUsers(
        [FromQuery] string? reviewStatus,
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
            return Ok(await reviewService.ListUsersAsync(
                reviewStatus, effectivePage, effectivePageSize, sortBy, sortOrder, cancellationToken));
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
