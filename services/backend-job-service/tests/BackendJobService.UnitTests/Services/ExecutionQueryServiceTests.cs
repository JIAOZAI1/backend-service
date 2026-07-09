using BackendJobService.Application.Exceptions;
using BackendJobService.Application.Interfaces;
using BackendJobService.Application.Services;
using BackendJobService.Domain.Entities;
using Moq;
using Shouldly;
using Xunit;

namespace BackendJobService.UnitTests.Services;

public class ExecutionQueryServiceTests
{
    private readonly Mock<IJobExecutionRepository> _executionRepository = new();
    private readonly Mock<IJobRepository> _jobRepository = new();
    private readonly ExecutionQueryService _sut;

    public ExecutionQueryServiceTests()
    {
        _sut = new ExecutionQueryService(_executionRepository.Object, _jobRepository.Object);
    }

    [Fact]
    public async Task ListExecutionsByJobAsync_ReturnsPagedResultOrderedByTriggeredAt()
    {
        var executions = new List<JobExecution>
        {
            new() { Id = 2, JobId = 1, TriggeredAt = new DateTime(2026, 1, 2) },
            new() { Id = 1, JobId = 1, TriggeredAt = new DateTime(2026, 1, 1) },
        };
        _executionRepository
            .Setup(r => r.ListPagedByJobIdAsync(1, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((executions, executions.Count));

        var result = await _sut.ListExecutionsByJobAsync(1, 1, 20, CancellationToken.None);

        result.Items.Select(e => e.Id).ShouldBe([2, 1]);
        result.Page.ShouldBe(1);
        result.PageSize.ShouldBe(20);
        result.Total.ShouldBe(2);
    }

    [Fact]
    public async Task GetExecutionAsync_NotFound_ThrowsNotFoundException()
    {
        _executionRepository
            .Setup(r => r.GetWithTaskExecutionsByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobExecution?)null);

        await Should.ThrowAsync<NotFoundException>(() => _sut.GetExecutionAsync(999, CancellationToken.None));
    }
}
