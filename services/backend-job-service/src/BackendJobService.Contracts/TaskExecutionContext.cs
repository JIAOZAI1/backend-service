namespace BackendJobService.Contracts;

public class TaskExecutionContext
{
    public long JobId { get; init; }
    public long JobTaskId { get; init; }
    public long JobExecutionId { get; init; }
    public long TaskExecutionId { get; init; }

    /// <summary>JobTask.ParametersJson 原样透传，插件自行反序列化为强类型或按需读取字段。</summary>
    public required string ParametersJson { get; init; }

    /// <summary>当前是第几次尝试，从 1 开始。插件可据此判断是否需要跳过已完成的幂等副作用。</summary>
    public int AttemptNumber { get; init; } = 1;
}
