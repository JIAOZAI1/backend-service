using BackendJobService.Application.Interfaces;
using BackendJobService.Application.Validators;
using BackendJobService.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BackendJobService.Infrastructure.Scheduling;

public class JobSchedulerOptions
{
    public const string SectionName = "JobScheduler";

    /// <summary>轮询到期作业的间隔。</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(10);
}

/// <summary>
/// Job Center：定期扫描到期的 Job，校验后创建 JobExecution 记录并投递到消息队列，
/// 由 TaskWorkerHostedService 消费执行。对应架构描述里的“b) job Center”。
/// </summary>
public class JobSchedulerHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<JobSchedulerOptions> options,
    ILogger<JobSchedulerHostedService> logger) : BackgroundService
{
    private readonly JobSchedulerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.PollInterval);

        do
        {
            try
            {
                await ScanAndDispatchDueJobsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Job scheduler scan failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ScanAndDispatchDueJobsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var jobRepository = scope.ServiceProvider.GetRequiredService<IJobRepository>();
        var executionRepository = scope.ServiceProvider.GetRequiredService<IJobExecutionRepository>();
        var publisher = scope.ServiceProvider.GetRequiredService<ITaskExecutionPublisher>();

        var now = DateTime.UtcNow;
        var dueJobs = await jobRepository.ListDueJobsAsync(now, cancellationToken);

        foreach (var job in dueJobs)
        {
            if (job.Tasks.Count == 0)
            {
                logger.LogWarning("Job {JobId} has no tasks configured, skipping trigger", job.Id);
                AdvanceNextRunAt(job, now);
                continue;
            }

            var execution = new JobExecution
            {
                JobId = job.Id,
                Status = JobExecutionStatus.Pending,
                TriggeredAt = now,
            };
            await executionRepository.AddAsync(execution, cancellationToken);
            await executionRepository.SaveChangesAsync(cancellationToken);

            await publisher.PublishAsync(execution.Id, cancellationToken);

            logger.LogInformation("Dispatched JobExecution {ExecutionId} for Job {JobId}", execution.Id, job.Id);

            AdvanceNextRunAt(job, now);
        }

        if (dueJobs.Count > 0)
        {
            await jobRepository.SaveChangesAsync(cancellationToken);
        }
    }

    private static void AdvanceNextRunAt(Job job, DateTime now)
    {
        if (job.ScheduleType == JobScheduleType.OneTime)
        {
            // 一次性作业触发后禁用，不再重复调度
            job.Status = JobStatus.Disabled;
            job.NextRunAt = null;
        }
        else
        {
            job.NextRunAt = JobValidator.ComputeNextRunAt(job, now);
        }
        job.UpdatedAt = now;
    }
}
