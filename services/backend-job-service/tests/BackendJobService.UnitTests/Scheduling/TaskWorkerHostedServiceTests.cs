using BackendJobService.Infrastructure.Scheduling;
using Shouldly;
using Xunit;

namespace BackendJobService.UnitTests.Scheduling;

public class TaskWorkerHostedServiceTests
{
    // 回归测试：output_json 是数据库原生 JSON 列，2026-07-09 联调时发现插件返回
    // 非 JSON 字符串会导致 SaveChanges 抛异常，进而让整个 JobExecution 卡在 Running
    // 状态永远不会完成（异常发生在成功路径的保存阶段，未被当时的 catch 块覆盖）。

    [Fact]
    public void NormalizeToJsonOrNull_ValidJson_ReturnsUnchanged()
    {
        var result = TaskWorkerHostedService.NormalizeToJsonOrNull("{\"key\":\"value\"}");

        result.ShouldBe("{\"key\":\"value\"}");
    }

    [Fact]
    public void NormalizeToJsonOrNull_PlainString_WrapsAsJson()
    {
        var result = TaskWorkerHostedService.NormalizeToJsonOrNull("not json at all");

        result.ShouldNotBeNull();
        result.ShouldContain("not json at all");
        // 结果本身必须是合法 JSON，否则等于没修
        Should.NotThrow(() => System.Text.Json.JsonDocument.Parse(result!));
    }

    [Fact]
    public void NormalizeToJsonOrNull_Null_ReturnsNull()
    {
        TaskWorkerHostedService.NormalizeToJsonOrNull(null).ShouldBeNull();
    }

    [Fact]
    public void NormalizeToJsonOrNull_EmptyString_ReturnsNull()
    {
        TaskWorkerHostedService.NormalizeToJsonOrNull("").ShouldBeNull();
    }

    [Fact]
    public void NormalizeToJsonOrNull_JsonArray_ReturnsUnchanged()
    {
        var result = TaskWorkerHostedService.NormalizeToJsonOrNull("[1,2,3]");

        result.ShouldBe("[1,2,3]");
    }

    [Fact]
    public void NormalizeToJsonOrNull_JsonNumber_ReturnsUnchanged()
    {
        // 合法 JSON 标量（数字/字符串/布尔）也应该被当成合法 JSON 放行
        var result = TaskWorkerHostedService.NormalizeToJsonOrNull("42");

        result.ShouldBe("42");
    }
}
