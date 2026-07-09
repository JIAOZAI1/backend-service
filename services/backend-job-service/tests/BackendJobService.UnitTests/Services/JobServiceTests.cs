using BackendJobService.Application.Common;
using BackendJobService.Application.DTOs;
using BackendJobService.Application.Exceptions;
using BackendJobService.Application.Interfaces;
using BackendJobService.Application.Services;
using BackendJobService.Domain.Entities;
using Moq;
using Shouldly;
using Xunit;

namespace BackendJobService.UnitTests.Services;

public class JobServiceTests
{
    private readonly Mock<IJobRepository> _jobRepository = new();
    private readonly JobService _sut;

    public JobServiceTests()
    {
        _sut = new JobService(_jobRepository.Object);
    }

    [Fact]
    public async Task CreateJobAsync_Cron_ComputesNextRunAt()
    {
        _jobRepository
            .Setup(r => r.AddAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new CreateJobRequest
        {
            Name = "daily-report",
            ScheduleType = JobScheduleType.Cron,
            CronExpression = "0 0 * * *",
        };

        var result = await _sut.CreateJobAsync(request, CancellationToken.None);

        result.Name.ShouldBe("daily-report");
        result.NextRunAt.ShouldNotBeNull();
        _jobRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateJobAsync_InvalidCron_ThrowsValidationException()
    {
        var request = new CreateJobRequest
        {
            Name = "bad-job",
            ScheduleType = JobScheduleType.Cron,
            CronExpression = "garbage",
        };

        await Should.ThrowAsync<ValidationException>(() => _sut.CreateJobAsync(request, CancellationToken.None));
        _jobRepository.Verify(r => r.AddAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateJobTaskAsync_JobNotFound_ThrowsNotFoundException()
    {
        _jobRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Job?)null);

        var request = new CreateJobTaskRequest
        {
            Name = "step-1",
            Order = 1,
            HandlerType = "MyPlugin.Handlers.FetchDataHandler",
            PluginAssembly = "MyPlugin.dll",
        };

        await Should.ThrowAsync<NotFoundException>(() => _sut.CreateJobTaskAsync(999, request, CancellationToken.None));
    }

    [Fact]
    public async Task CreateJobTaskAsync_JobExists_AddsTask()
    {
        var job = new Job { Id = 1, Name = "daily-report" };
        _jobRepository
            .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);
        _jobRepository
            .Setup(r => r.AddTaskAsync(It.IsAny<JobTask>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobTask t, CancellationToken _) => t);

        var request = new CreateJobTaskRequest
        {
            Name = "step-1",
            Order = 1,
            HandlerType = "MyPlugin.Handlers.FetchDataHandler",
            PluginAssembly = "MyPlugin.dll",
        };

        var result = await _sut.CreateJobTaskAsync(1, request, CancellationToken.None);

        result.Name.ShouldBe("step-1");
        result.JobId.ShouldBe(1);
        _jobRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateJobAsync_JobExists_UpdatesFieldsAndRecomputesNextRunAt()
    {
        var job = new Job { Id = 1, Name = "old-name", ScheduleType = JobScheduleType.OneTime, RunAt = DateTime.UtcNow.AddDays(1), Status = JobStatus.Enabled };
        _jobRepository
            .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var request = new UpdateJobRequest
        {
            Name = "daily-report",
            ScheduleType = JobScheduleType.Cron,
            CronExpression = "0 0 * * *",
            Status = JobStatus.Enabled,
        };

        var result = await _sut.UpdateJobAsync(1, request, CancellationToken.None);

        result.Name.ShouldBe("daily-report");
        result.NextRunAt.ShouldNotBeNull();
        _jobRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateJobAsync_Disabled_ClearsNextRunAt()
    {
        var job = new Job { Id = 1, Name = "daily-report", ScheduleType = JobScheduleType.Cron, CronExpression = "0 0 * * *", Status = JobStatus.Enabled, NextRunAt = DateTime.UtcNow };
        _jobRepository
            .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var request = new UpdateJobRequest
        {
            Name = "daily-report",
            ScheduleType = JobScheduleType.Cron,
            CronExpression = "0 0 * * *",
            Status = JobStatus.Disabled,
        };

        var result = await _sut.UpdateJobAsync(1, request, CancellationToken.None);

        result.Status.ShouldBe(JobStatus.Disabled);
        result.NextRunAt.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateJobAsync_NotFound_ThrowsNotFoundException()
    {
        _jobRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Job?)null);

        var request = new UpdateJobRequest { Name = "x", ScheduleType = JobScheduleType.Cron, CronExpression = "0 0 * * *", Status = JobStatus.Enabled };

        await Should.ThrowAsync<NotFoundException>(() => _sut.UpdateJobAsync(999, request, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateJobAsync_InvalidCron_ThrowsValidationException()
    {
        var job = new Job { Id = 1, Name = "daily-report" };
        _jobRepository
            .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var request = new UpdateJobRequest { Name = "daily-report", ScheduleType = JobScheduleType.Cron, CronExpression = "garbage", Status = JobStatus.Enabled };

        await Should.ThrowAsync<ValidationException>(() => _sut.UpdateJobAsync(1, request, CancellationToken.None));
        _jobRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteJobAsync_JobExists_SetsDeletedAt()
    {
        var job = new Job { Id = 1, Name = "daily-report" };
        _jobRepository
            .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        await _sut.DeleteJobAsync(1, CancellationToken.None);

        job.DeletedAt.ShouldNotBeNull();
        _jobRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteJobAsync_NotFound_ThrowsNotFoundException()
    {
        _jobRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Job?)null);

        await Should.ThrowAsync<NotFoundException>(() => _sut.DeleteJobAsync(999, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateJobTaskAsync_TaskExists_UpdatesFields()
    {
        var task = new JobTask { Id = 1, JobId = 1, Name = "old", Order = 1, HandlerType = "H1", PluginAssembly = "p.dll" };
        _jobRepository
            .Setup(r => r.GetTaskByIdAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        var request = new UpdateJobTaskRequest
        {
            Name = "renamed",
            Order = 2,
            HandlerType = "H2",
            PluginAssembly = "p2.dll",
        };

        var result = await _sut.UpdateJobTaskAsync(1, 1, request, CancellationToken.None);

        result.Name.ShouldBe("renamed");
        result.Order.ShouldBe(2);
        _jobRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateJobTaskAsync_NotFound_ThrowsNotFoundException()
    {
        _jobRepository
            .Setup(r => r.GetTaskByIdAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobTask?)null);

        var request = new UpdateJobTaskRequest { Name = "x", Order = 1, HandlerType = "H1", PluginAssembly = "p.dll" };

        await Should.ThrowAsync<NotFoundException>(() => _sut.UpdateJobTaskAsync(1, 999, request, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteJobTaskAsync_TaskExists_SetsDeletedAt()
    {
        var task = new JobTask { Id = 1, JobId = 1, Name = "step-1" };
        _jobRepository
            .Setup(r => r.GetTaskByIdAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        await _sut.DeleteJobTaskAsync(1, 1, CancellationToken.None);

        task.DeletedAt.ShouldNotBeNull();
        _jobRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteJobTaskAsync_NotFound_ThrowsNotFoundException()
    {
        _jobRepository
            .Setup(r => r.GetTaskByIdAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobTask?)null);

        await Should.ThrowAsync<NotFoundException>(() => _sut.DeleteJobTaskAsync(1, 999, CancellationToken.None));
    }

    [Fact]
    public async Task GetJobAsync_NotFound_ThrowsNotFoundException()
    {
        _jobRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Job?)null);

        await Should.ThrowAsync<NotFoundException>(() => _sut.GetJobAsync(1, CancellationToken.None));
    }

    [Fact]
    public async Task ListJobTasksAsync_JobExists_ReturnsPagedTasks()
    {
        var job = new Job { Id = 1 };
        var tasks = new List<JobTask>
        {
            new() { Id = 1, JobId = 1, Name = "first", Order = 1, HandlerType = "H1", PluginAssembly = "p.dll" },
            new() { Id = 2, JobId = 1, Name = "second", Order = 2, HandlerType = "H2", PluginAssembly = "p.dll" },
        };
        _jobRepository
            .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);
        _jobRepository
            .Setup(r => r.ListTasksPagedAsync(1, 1, 20, It.IsAny<SortSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tasks, tasks.Count));

        var result = await _sut.ListJobTasksAsync(1, 1, 20, sortBy: null, SortOrder.Asc, CancellationToken.None);

        result.Items.Select(t => t.Name).ShouldBe(["first", "second"]);
        result.Total.ShouldBe(2);
    }

    [Fact]
    public async Task ListJobTasksAsync_JobNotFound_ThrowsNotFoundException()
    {
        _jobRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Job?)null);

        await Should.ThrowAsync<NotFoundException>(() => _sut.ListJobTasksAsync(999, 1, 20, sortBy: null, SortOrder.Asc, CancellationToken.None));
    }

    [Fact]
    public async Task ListJobTasksAsync_InvalidSortBy_ThrowsValidationException()
    {
        var job = new Job { Id = 1 };
        _jobRepository
            .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        await Should.ThrowAsync<ValidationException>(() => _sut.ListJobTasksAsync(1, 1, 20, sortBy: "notAField", SortOrder.Asc, CancellationToken.None));
    }

    [Fact]
    public async Task ListJobsAsync_InvalidSortBy_ThrowsValidationException()
    {
        await Should.ThrowAsync<ValidationException>(() => _sut.ListJobsAsync(1, 20, sortBy: "notAField", SortOrder.Asc, CancellationToken.None));
    }

    [Fact]
    public async Task ListJobsAsync_ValidSortBy_ReturnsPagedJobs()
    {
        var jobs = new List<Job> { new() { Id = 1, Name = "b" }, new() { Id = 2, Name = "a" } };
        _jobRepository
            .Setup(r => r.ListPagedAsync(1, 20, It.IsAny<SortSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((jobs, jobs.Count));

        var result = await _sut.ListJobsAsync(1, 20, sortBy: "name", SortOrder.Asc, CancellationToken.None);

        result.Total.ShouldBe(2);
    }
}
