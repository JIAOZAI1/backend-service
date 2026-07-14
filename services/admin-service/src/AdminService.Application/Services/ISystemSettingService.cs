using AdminService.Application.DTOs;

namespace AdminService.Application.Services;

public interface ISystemSettingService
{
    Task<List<SystemSettingResponse>> ListSettingsAsync(CancellationToken cancellationToken);
    Task<SystemSettingResponse> GetSettingAsync(string key, CancellationToken cancellationToken);
    Task<SystemSettingResponse> UpsertSettingAsync(string key, UpsertSystemSettingRequest request, CancellationToken cancellationToken);
}
