using AdminService.Application.Common;
using AdminService.Application.DTOs;
using AdminService.Application.Exceptions;
using AdminService.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AdminService.Api.Controllers;

/// <summary>
/// 系统管理员用户管理：查询全量用户（含角色、租户信息）、重置密码。数据来自 sso-service
/// 拥有的 users/roles/user_roles 表与本服务拥有的 tenants/user_tenants 表，两服务共用
/// 同一个 MySQL 数据库，本服务直接跨表查询/写入，不经 HTTP 调用 sso-service。
/// </summary>
[ApiController]
[Route("admin-service/api/v1/users")]
public class UsersController(IUserManagementService userManagementService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<UserWithTenantResponse>>> ListUsers(
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
            return Ok(await userManagementService.ListUsersAsync(
                effectivePage, effectivePageSize, sortBy, sortOrder, cancellationToken));
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>重置指定用户的密码为随机临时密码，明文只在本次响应里返回一次，管理员需当场转告用户。</summary>
    [HttpPost("{userId}/reset-password")]
    public async Task<ActionResult<ResetPasswordResponse>> ResetPassword(ulong userId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await userManagementService.ResetPasswordAsync(userId, cancellationToken));
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>按 ID 查询任意用户详情，不限 reviewStatus/租户状态。</summary>
    [HttpGet("{userId}")]
    public async Task<ActionResult<UserDetailResponse>> GetUser(ulong userId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await userManagementService.GetUserAsync(userId, cancellationToken));
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>启用用户，幂等：已启用的用户重复调用不报错。</summary>
    [HttpPost("{userId}/enable")]
    public async Task<IActionResult> EnableUser(ulong userId, CancellationToken cancellationToken)
    {
        try
        {
            await userManagementService.SetUserEnabledAsync(userId, enabled: true, cancellationToken);
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>禁用用户，幂等：已禁用的用户重复调用不报错。禁用后该用户下次登录会被 sso-service 拒绝。</summary>
    [HttpPost("{userId}/disable")]
    public async Task<IActionResult> DisableUser(ulong userId, CancellationToken cancellationToken)
    {
        try
        {
            await userManagementService.SetUserEnabledAsync(userId, enabled: false, cancellationToken);
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
