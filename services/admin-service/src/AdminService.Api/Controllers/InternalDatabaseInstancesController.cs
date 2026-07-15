using AdminService.Application.DTOs;
using AdminService.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AdminService.Api.Controllers;

/// <summary>
/// 仅供集群内直连调用（规范第 16.5 章），路由不带 admin-service 网关前缀，不经网关暴露，
/// 由 RequireInternalTokenMiddleware 校验 X-Internal-Token（见 Program.cs 的 /internal 分支）。
/// 供 backend-job-service 的建库/建用户插件按 databaseInstanceId 现取解密后的连接信息。
/// </summary>
[ApiController]
[Route("internal/database-instances")]
public class InternalDatabaseInstancesController(
    IDatabaseInstanceRepository databaseInstanceRepository,
    IDbCredentialCipher cipher) : ControllerBase
{
    [HttpGet("{id}/credentials")]
    public async Task<ActionResult<DatabaseInstanceCredentialsResponse>> GetCredentialsInternal(
        long id, CancellationToken cancellationToken)
    {
        var instance = await databaseInstanceRepository.GetByIdAsync(id, cancellationToken);
        if (instance is null)
        {
            return NotFound(new { error = $"database instance {id} not found" });
        }

        return Ok(new DatabaseInstanceCredentialsResponse
        {
            Id = instance.Id,
            DbType = instance.DbType.ToString().ToLowerInvariant(),
            Host = instance.Host,
            Port = instance.Port,
            Username = instance.Username,
            Password = cipher.Decrypt(instance.EncryptedPassword),
        });
    }
}
