using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BackendJobService.Contracts;
using BackendJobService.Plugins.Internal;
using BackendJobService.Plugins.MySql;

namespace BackendJobService.Plugins.Sso;

/// <summary>
/// 调用 sso-service 的 PUT /internal/users/{userId}/review，把用户标记为已审核。sso-service 侧本身
/// 幂等（重复调用已审核用户不报错），本插件不额外做幂等短路。
///
/// parameters_json:
/// {
///   "userId": 42,       // 必填
///   "reviewedBy": 1     // 必填，审核人 ID
/// }
///
/// sso-service 的 base URL 与共享密钥从环境变量读取，见 InternalServiceClientHelper。
/// </summary>
[TaskPlugin("sso-mark-user-reviewed",
    Description = "调用 sso-service 标记用户已审核",
    Version = "1.0.0")]
public class MarkUserReviewedHandler : ITaskHandler
{
    public const string SsoServiceBaseUrlEnvVar = "JOB_PLUGIN_SSO_SERVICE_BASE_URL";

    private readonly Func<HttpClient> _clientFactory;

    public MarkUserReviewedHandler() : this(() => InternalServiceClientHelper.CreateClient(SsoServiceBaseUrlEnvVar)) { }

    /// <summary>测试注入点：替换 HttpClient 来源。</summary>
    internal MarkUserReviewedHandler(Func<HttpClient> clientFactory)
    {
        _clientFactory = clientFactory;
    }

    private sealed record Parameters(ulong? UserId, ulong? ReviewedBy);

    public async Task<TaskResult> ExecuteAsync(TaskExecutionContext context, CancellationToken cancellationToken)
    {
        Parameters? parameters;
        try
        {
            parameters = JsonSerializer.Deserialize<Parameters>(context.ParametersJson, MySqlPluginHelper.JsonOptions);
        }
        catch (JsonException ex)
        {
            return TaskResult.Fail($"parameters_json 不是合法 JSON: {ex.Message}");
        }

        if (parameters?.UserId is null)
        {
            return TaskResult.Fail("缺少必填参数 userId");
        }

        if (parameters.ReviewedBy is null)
        {
            return TaskResult.Fail("缺少必填参数 reviewedBy");
        }

        HttpClient client;
        try
        {
            client = _clientFactory();
        }
        catch (InvalidOperationException ex)
        {
            return TaskResult.Fail(ex.Message);
        }

        try
        {
            using (client)
            {
                var response = await client.PutAsJsonAsync(
                    $"/internal/users/{parameters.UserId}/review",
                    new { reviewedBy = parameters.ReviewedBy },
                    cancellationToken);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return TaskResult.Fail($"sso-service: user {parameters.UserId} not found");
                }

                response.EnsureSuccessStatusCode();
                return TaskResult.Ok();
            }
        }
        catch (HttpRequestException ex)
        {
            return TaskResult.Fail($"调用 sso-service 失败: {ex.Message}");
        }
    }
}
