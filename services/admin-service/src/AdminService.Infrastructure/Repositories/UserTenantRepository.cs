using AdminService.Application.Interfaces;
using AdminService.Domain.Entities;
using AdminService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdminService.Infrastructure.Repositories;

public class UserTenantRepository(AdminDbContext dbContext) : IUserTenantRepository
{
    public Task<UserTenant?> GetByUserIdAsync(ulong userId, CancellationToken cancellationToken) =>
        dbContext.UserTenants.FirstOrDefaultAsync(ut => ut.UserId == userId, cancellationToken);

    public async Task AddAsync(UserTenant userTenant, CancellationToken cancellationToken) =>
        await dbContext.UserTenants.AddAsync(userTenant, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
