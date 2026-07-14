using AdminService.Application.DTOs;
using AdminService.Application.Exceptions;
using AdminService.Application.Interfaces;
using AdminService.Domain.Entities;

namespace AdminService.Application.Services;

public class SystemSettingService(ISystemSettingRepository repository) : ISystemSettingService
{
    public async Task<List<SystemSettingResponse>> ListSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await repository.ListAsync(cancellationToken);
        return settings.Select(ToResponse).ToList();
    }

    public async Task<SystemSettingResponse> GetSettingAsync(string key, CancellationToken cancellationToken)
    {
        var setting = await repository.GetByKeyAsync(key, cancellationToken)
            ?? throw new SettingNotFoundException(key);
        return ToResponse(setting);
    }

    public async Task<SystemSettingResponse> UpsertSettingAsync(string key, UpsertSystemSettingRequest request, CancellationToken cancellationToken)
    {
        var existing = await repository.GetByKeyAsync(key, cancellationToken);
        var now = DateTime.UtcNow;

        if (existing is null)
        {
            var setting = new SystemSetting
            {
                Key = key,
                Value = request.Value,
                Description = request.Description,
                CreatedAt = now,
                UpdatedAt = now,
            };
            await repository.AddAsync(setting, cancellationToken);
            return ToResponse(setting);
        }

        existing.Value = request.Value;
        existing.Description = request.Description;
        existing.UpdatedAt = now;
        await repository.UpdateAsync(existing, cancellationToken);
        return ToResponse(existing);
    }

    private static SystemSettingResponse ToResponse(SystemSetting setting) =>
        new(setting.Id, setting.Key, setting.Value, setting.Description, setting.UpdatedAt);
}
