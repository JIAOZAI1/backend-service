using AdminService.Domain.Entities;

namespace AdminService.Application.Interfaces;

public interface ISystemSettingRepository
{
    Task<List<SystemSetting>> ListAsync(CancellationToken cancellationToken);
    Task<SystemSetting?> GetByKeyAsync(string key, CancellationToken cancellationToken);
    Task AddAsync(SystemSetting setting, CancellationToken cancellationToken);
    Task UpdateAsync(SystemSetting setting, CancellationToken cancellationToken);
}
