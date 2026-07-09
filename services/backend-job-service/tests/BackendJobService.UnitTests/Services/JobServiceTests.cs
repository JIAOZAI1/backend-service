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
    public async Task GetJobAsync_NotFound_ThrowsNotFoundException()
    {
        _jobRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Job?)null);

        await Should.ThrowAsync<NotFoundException>(() => _sut.GetJobAsync(1, CancellationToken.None));
    }

    [Fact]
    public async Task ListJobTasksAsync_ReturnsTasksOrderedByOrder()
    {
        var job = new Job
        {
            Id = 1,
            Tasks =
            [
                new JobTask { Id = 3, JobId = 1, Name = "third", Order = 3, HandlerType = "H3", PluginAssembly = "p.dll" },
                new JobTask { Id = 1, JobId = 1, Name = "first", Order = 1, HandlerType = "H1", PluginAssembly = "p.dll" },
                new JobTask { Id = 2, JobId = 1, Name = "second", Order = 2, HandlerType = "H2", PluginAssembly = "p.dll" },
            ],
        };
        _jobRepository
            .Setup(r => r.GetWithTasksByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var result = await _sut.ListJobTasksAsync(1, CancellationToken.None);

        result.Select(t => t.Name).ShouldBe(["first", "second", "third"]);
    }
}
