using BackendJobService.Application.DTOs;
using BackendJobService.Application.Exceptions;
using BackendJobService.Application.Validators;
using BackendJobService.Domain.Entities;
using Shouldly;
using Xunit;

namespace BackendJobService.UnitTests.Validators;

public class JobValidatorTests
{
    [Fact]
    public void ValidateCreateRequest_Cron_WithValidExpression_DoesNotThrow()
    {
        var request = new CreateJobRequest
        {
            Name = "daily-report",
            ScheduleType = JobScheduleType.Cron,
            CronExpression = "0 0 * * *",
        };

        Should.NotThrow(() => JobValidator.ValidateCreateRequest(request));
    }

    [Fact]
    public void ValidateCreateRequest_Cron_MissingExpression_Throws()
    {
        var request = new CreateJobRequest
        {
            Name = "daily-report",
            ScheduleType = JobScheduleType.Cron,
        };

        Should.Throw<ValidationException>(() => JobValidator.ValidateCreateRequest(request));
    }

    [Fact]
    public void ValidateCreateRequest_Cron_InvalidExpression_Throws()
    {
        var request = new CreateJobRequest
        {
            Name = "daily-report",
            ScheduleType = JobScheduleType.Cron,
            CronExpression = "not a cron expression",
        };

        Should.Throw<ValidationException>(() => JobValidator.ValidateCreateRequest(request));
    }

    [Fact]
    public void ValidateCreateRequest_Cron_WithRunAtSet_Throws()
    {
        var request = new CreateJobRequest
        {
            Name = "daily-report",
            ScheduleType = JobScheduleType.Cron,
            CronExpression = "0 0 * * *",
            RunAt = DateTime.UtcNow.AddHours(1),
        };

        Should.Throw<ValidationException>(() => JobValidator.ValidateCreateRequest(request));
    }

    [Fact]
    public void ValidateCreateRequest_OneTime_WithFutureRunAt_DoesNotThrow()
    {
        var request = new CreateJobRequest
        {
            Name = "one-off-migration",
            ScheduleType = JobScheduleType.OneTime,
            RunAt = DateTime.UtcNow.AddMinutes(10),
        };

        Should.NotThrow(() => JobValidator.ValidateCreateRequest(request));
    }

    [Fact]
    public void ValidateCreateRequest_OneTime_MissingRunAt_Throws()
    {
        var request = new CreateJobRequest
        {
            Name = "one-off-migration",
            ScheduleType = JobScheduleType.OneTime,
        };

        Should.Throw<ValidationException>(() => JobValidator.ValidateCreateRequest(request));
    }

    [Fact]
    public void ValidateCreateRequest_OneTime_PastRunAt_Throws()
    {
        var request = new CreateJobRequest
        {
            Name = "one-off-migration",
            ScheduleType = JobScheduleType.OneTime,
            RunAt = DateTime.UtcNow.AddMinutes(-10),
        };

        Should.Throw<ValidationException>(() => JobValidator.ValidateCreateRequest(request));
    }

    [Fact]
    public void ValidateCreateRequest_OneTime_WithCronExpressionSet_Throws()
    {
        var request = new CreateJobRequest
        {
            Name = "one-off-migration",
            ScheduleType = JobScheduleType.OneTime,
            RunAt = DateTime.UtcNow.AddMinutes(10),
            CronExpression = "0 0 * * *",
        };

        Should.Throw<ValidationException>(() => JobValidator.ValidateCreateRequest(request));
    }

    [Fact]
    public void ValidateUpdateRequest_Cron_WithValidExpression_DoesNotThrow()
    {
        var request = new UpdateJobRequest
        {
            Name = "daily-report",
            ScheduleType = JobScheduleType.Cron,
            CronExpression = "0 0 * * *",
            Status = JobStatus.Enabled,
        };

        Should.NotThrow(() => JobValidator.ValidateUpdateRequest(request));
    }

    [Fact]
    public void ValidateUpdateRequest_Cron_InvalidExpression_Throws()
    {
        var request = new UpdateJobRequest
        {
            Name = "daily-report",
            ScheduleType = JobScheduleType.Cron,
            CronExpression = "garbage",
            Status = JobStatus.Enabled,
        };

        Should.Throw<ValidationException>(() => JobValidator.ValidateUpdateRequest(request));
    }

    [Fact]
    public void ComputeNextRunAt_Cron_ReturnsNextOccurrenceAfterAsOf()
    {
        var job = new Job
        {
            ScheduleType = JobScheduleType.Cron,
            CronExpression = "0 0 * * *", // daily at midnight
        };
        var asOf = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var next = JobValidator.ComputeNextRunAt(job, asOf);

        next.ShouldNotBeNull();
        next!.Value.ShouldBeGreaterThan(asOf);
    }

    [Fact]
    public void ComputeNextRunAt_OneTime_ReturnsRunAt()
    {
        var runAt = DateTime.UtcNow.AddHours(2);
        var job = new Job
        {
            ScheduleType = JobScheduleType.OneTime,
            RunAt = runAt,
        };

        var next = JobValidator.ComputeNextRunAt(job, DateTime.UtcNow);

        next.ShouldBe(runAt);
    }
}
