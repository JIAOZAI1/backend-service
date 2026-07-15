using AdminService.Application.Exceptions;
using AdminService.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AdminService.Api.Controllers;

/// <summary>
/// 仅供集群内直连调用（规范第 16.5 章），路由不带 admin-service 网关前缀，不经网关暴露，
/// 由 RequireInternalTokenMiddleware 校验 X-Internal-Token（见 Program.cs 的 /internal 分支）。
/// 供 backend-job-service 的 admin-activate-tenant 插件在开户 Job 全部前置 Task 成功后回写租户状态。
/// </summary>
[ApiController]
[Route("internal/tenants")]
public class InternalTenantsController(ITenantInternalService tenantInternalService) : ControllerBase
{
    [HttpPut("{tenantId}/activate")]
    public async Task<IActionResult> ActivateInternal(string tenantId, CancellationToken cancellationToken)
    {
        try
        {
            await tenantInternalService.ActivateAsync(tenantId, cancellationToken);
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
