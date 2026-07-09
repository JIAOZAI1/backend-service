namespace BackendJobService.Contracts;

/// <summary>
/// 任务插件必须实现的接口。插件 DLL 只需引用本项目（Contracts），不需要引用
/// Worker 所在的完整服务代码，保持插件与宿主之间的最小耦合。
/// </summary>
public interface ITaskHandler
{
    Task<TaskResult> ExecuteAsync(TaskExecutionContext context, CancellationToken cancellationToken);
}
