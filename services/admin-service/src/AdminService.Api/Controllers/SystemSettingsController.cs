using AdminService.Application.DTOs;
using AdminService.Application.Exceptions;
using AdminService.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace AdminService.Api.Controllers;

[ApiController]
[Route("admin-service/api/v1/settings")]
public class SystemSettingsController(ISystemSettingService settingService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<SystemSettingResponse>>> ListSettings(CancellationToken cancellationToken)
    {
        return Ok(await settingService.ListSettingsAsync(cancellationToken));
    }

    [HttpGet("{key}")]
    public async Task<ActionResult<SystemSettingResponse>> GetSetting(string key, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await settingService.GetSettingAsync(key, cancellationToken));
        }
        catch (SettingNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPut("{key}")]
    public async Task<ActionResult<SystemSettingResponse>> UpsertSetting(
        string key,
        [FromBody] UpsertSystemSettingRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await settingService.UpsertSettingAsync(key, request, cancellationToken));
    }
}
