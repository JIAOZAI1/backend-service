using System.Text.Json;
using BackendJobService.Contracts;

namespace SamplePlugin;

/// <summary>
/// 示例插件：用于端到端联调验证反射加载与执行链路，不代表真实业务逻辑。
/// OutputJson 必须是合法 JSON（数据库 output_json 列是原生 JSON 类型）。
/// </summary>
public class EchoTaskHandler : ITaskHandler
{
    public Task<TaskResult> ExecuteAsync(TaskExecutionContext context, CancellationToken cancellationToken)
    {
        var output = JsonSerializer.Serialize(new
        {
            attempt = context.AttemptNumber,
            receivedParameters = context.ParametersJson,
        });
        return Task.FromResult(TaskResult.Ok(output));
    }
}
