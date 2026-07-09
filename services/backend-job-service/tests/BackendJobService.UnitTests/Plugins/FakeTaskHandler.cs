using BackendJobService.Contracts;

namespace BackendJobService.UnitTests.Plugins;

/// <summary>
/// 供 TaskHandlerLoaderTests 使用：真实存在于测试程序集里的一个 ITaskHandler 实现，
/// 用来验证反射加载/实例化逻辑，不需要额外编译一个独立插件项目。
/// </summary>
public class FakeTaskHandler : ITaskHandler
{
    public Task<TaskResult> ExecuteAsync(TaskExecutionContext context, CancellationToken cancellationToken) =>
        Task.FromResult(TaskResult.Ok("fake-output"));
}

public class NotATaskHandler;
