using AdminService.Application.Common;
using AdminService.Application.Interfaces;
using AdminService.Domain.Entities;
using AdminService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdminService.Infrastructure.Repositories;

public class UserManagementRepository(AdminDbContext dbContext) : IUserManagementRepository
{
    private static readonly Dictionary<string, Func<IQueryable<SsoUser>, SortOrder, IOrderedQueryable<SsoUser>>> _sorters =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = (q, o) => o == SortOrder.Asc ? q.OrderBy(u => u.Id) : q.OrderByDescending(u => u.Id),
            ["username"] = (q, o) => o == SortOrder.Asc ? q.OrderBy(u => u.Username) : q.OrderByDescending(u => u.Username),
            ["createdAt"] = (q, o) => o == SortOrder.Asc ? q.OrderBy(u => u.CreatedAt) : q.OrderByDescending(u => u.CreatedAt),
        };

    public async Task<(List<UserWithTenantRow> Items, long Total)> ListUsersWithTenantAsync(
        int page, int pageSize, SortSpec sort, CancellationToken cancellationToken)
    {
        if (!_sorters.TryGetValue(sort.SortBy, out var sorter))
        {
            throw new InvalidOperationException($"no sorter registered for field: {sort.SortBy}");
        }

        var query = sorter(dbContext.SsoUsers.AsNoTracking(), sort.SortOrder);
        var total = await query.LongCountAsync(cancellationToken);
        var users = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        if (users.Count == 0)
        {
            return ([], total);
        }

        var userIds = users.Select(u => u.Id).ToList();

        // 角色单独批量查询再在内存里聚合——EF Core 对 MySQL 没有可移植的 GROUP_CONCAT
        // 映射，拆成两次查询比拼原生 SQL 更符合仓库其他 Repository 的实现风格。
        var userRoles = await (
            from ur in dbContext.SsoUserRoles.AsNoTracking()
            join r in dbContext.SsoRoles.AsNoTracking() on ur.RoleId equals r.Id
            where userIds.Contains(ur.UserId)
            select new { ur.UserId, RoleName = r.Name }
        ).ToListAsync(cancellationToken);
        var rolesByUserId = userRoles
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.RoleName).ToList());

        // 租户信息只取 active 状态，同 sso-service 的 GetActiveTenantCodeByUserID 语义一致。
        var tenantInfoByUserId = await (
            from ut in dbContext.UserTenants.AsNoTracking()
            join t in dbContext.Tenants.AsNoTracking() on ut.TenantId equals t.Id
            where userIds.Contains(ut.UserId) && t.Status == TenantStatus.Active
            select new { ut.UserId, t.TenantCode, t.LicenseExpiresAt }
        ).ToDictionaryAsync(x => x.UserId, x => (x.TenantCode, x.LicenseExpiresAt), cancellationToken);

        var items = users.Select(u =>
        {
            rolesByUserId.TryGetValue(u.Id, out var roles);
            tenantInfoByUserId.TryGetValue(u.Id, out var tenantInfo);
            return new UserWithTenantRow(
                u.Id, u.Username, u.Email,
                roles ?? [],
                tenantInfo.TenantCode, tenantInfo.LicenseExpiresAt,
                u.CreatedAt);
        }).ToList();

        return (items, total);
    }

    public Task<SsoUser?> GetUserByIdAsync(ulong userId, CancellationToken cancellationToken) =>
        dbContext.SsoUsers.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
