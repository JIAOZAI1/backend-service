namespace BackendJobService.Domain.Entities;

/// <summary>
/// Job 下的一个执行步骤。Worker 按 Order 顺序依次执行同一 Job 下的所有 Task。
/// </summary>
public class JobTask
{
    public long Id { get; set; }
    public long JobId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>顺序步骤号，Worker 按此字段升序依次执行。</summary>
    public int Order { get; set; }

    /// <summary>插件里 ITaskHandler 实现类的完整类型名（含命名空间），用于反射实例化。</summary>
    public string HandlerType { get; set; } = string.Empty;

    /// <summary>加载该 HandlerType 所在的插件程序集文件名（plugins/ 目录下）。</summary>
    public string PluginAssembly { get; set; } = string.Empty;

    /// <summary>传给 ITaskHandler.ExecuteAsync 的参数，JSON 格式，由 Handler 自行反序列化。</summary>
    public string ParametersJson { get; set; } = "{}";

    public int TimeoutSeconds { get; set; } = 300;
    public int MaxRetryCount { get; set; } = 0;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public Job? Job { get; set; }
}
