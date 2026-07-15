using AdminService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AdminService.Infrastructure.Persistence;

public class AdminDbContext(DbContextOptions<AdminDbContext> options) : DbContext(options)
{
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<UserTenant> UserTenants => Set<UserTenant>();
    public DbSet<DatabaseInstance> DatabaseInstances => Set<DatabaseInstance>();

    // sso-service 拥有的表，本服务只读/仅密码重置写，见 SsoUser.cs 顶部说明。
    public DbSet<SsoUser> SsoUsers => Set<SsoUser>();
    public DbSet<SsoRole> SsoRoles => Set<SsoRole>();
    public DbSet<SsoUserRole> SsoUserRoles => Set<SsoUserRole>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AdminDbContext).Assembly);
    }
}
