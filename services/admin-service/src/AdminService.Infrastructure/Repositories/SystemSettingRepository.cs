using AdminService.Application.Interfaces;
using AdminService.Domain.Entities;
using AdminService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdminService.Infrastructure.Repositories;

public class SystemSettingRepository(AdminDbContext dbContext) : ISystemSettingRepository
{
    public Task<List<SystemSetting>> ListAsync(CancellationToken cancellationToken) =>
        dbContext.SystemSettings.AsNoTracking().OrderBy(s => s.Key).ToListAsync(cancellationToken);

    public Task<SystemSetting?> GetByKeyAsync(string key, CancellationToken cancellationToken) =>
        dbContext.SystemSettings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

    public async Task AddAsync(SystemSetting setting, CancellationToken cancellationToken)
    {
        dbContext.SystemSettings.Add(setting);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(SystemSetting setting, CancellationToken cancellationToken)
    {
        dbContext.SystemSettings.Update(setting);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
