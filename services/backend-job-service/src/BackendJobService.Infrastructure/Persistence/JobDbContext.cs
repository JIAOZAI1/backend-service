using BackendJobService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BackendJobService.Infrastructure.Persistence;

public class JobDbContext(DbContextOptions<JobDbContext> options) : DbContext(options)
{
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobTask> JobTasks => Set<JobTask>();
    public DbSet<JobExecution> JobExecutions => Set<JobExecution>();
    public DbSet<TaskExecution> TaskExecutions => Set<TaskExecution>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JobDbContext).Assembly);
    }
}
