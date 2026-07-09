using BackendJobService.Application.DTOs;
using BackendJobService.Application.Exceptions;
using BackendJobService.Domain.Entities;
using NCrontab;

namespace BackendJobService.Application.Validators;

/// <summary>
/// Job 校验：Cron 表达式合法性、OneTime 时间点合理性——对应架构描述里 Job Center 的
/// “作业校验”职责，放在 Application 层而不是 Domain，因为依赖 NCrontab 这类具体解析库。
/// </summary>
public static class JobValidator
{
    public static void ValidateCreateRequest(CreateJobRequest request) =>
        ValidateSchedule(request.ScheduleType, request.CronExpression, request.RunAt);

    public static void ValidateUpdateRequest(UpdateJobRequest request) =>
        ValidateSchedule(request.ScheduleType, request.CronExpression, request.RunAt);

    private static void ValidateSchedule(JobScheduleType scheduleType, string? cronExpression, DateTime? runAt)
    {
        switch (scheduleType)
        {
            case JobScheduleType.Cron:
                if (string.IsNullOrWhiteSpace(cronExpression))
                {
                    throw new ValidationException("cronExpression is required when scheduleType is Cron");
                }
                if (runAt is not null)
                {
                    throw new ValidationException("runAt must not be set when scheduleType is Cron");
                }
                TryParseCron(cronExpression);
                break;

            case JobScheduleType.OneTime:
                if (runAt is null)
                {
                    throw new ValidationException("runAt is required when scheduleType is OneTime");
                }
                if (cronExpression is not null)
                {
                    throw new ValidationException("cronExpression must not be set when scheduleType is OneTime");
                }
                if (runAt <= DateTime.UtcNow)
                {
                    throw new ValidationException("runAt must be in the future");
                }
                break;

            default:
                throw new ValidationException($"unsupported scheduleType: {scheduleType}");
        }
    }

    public static DateTime? ComputeNextRunAt(Job job, DateTime asOf)
    {
        return job.ScheduleType switch
        {
            JobScheduleType.Cron => TryParseCron(job.CronExpression!).GetNextOccurrence(asOf),
            JobScheduleType.OneTime => job.RunAt,
            _ => null,
        };
    }

    private static CrontabSchedule TryParseCron(string expression)
    {
        try
        {
            return CrontabSchedule.Parse(expression, new CrontabSchedule.ParseOptions { IncludingSeconds = false });
        }
        catch (Exception ex)
        {
            throw new ValidationException($"invalid cron expression: {ex.Message}");
        }
    }
}
