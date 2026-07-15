using System.Net;
using System.Text.Json;
using BackendJobService.Contracts;
using BackendJobService.Plugins.Internal;
using BackendJobService.Plugins.MySql;

namespace BackendJobService.Plugins.Admin;

/// <summary>
/// 调用 admin-service 的 PUT /internal/tenants/{tenantId}/activate，把租户状态置为 Active。
/// admin-service 侧幂等（已 Active 直接返回），本插件不额外做幂等短路。
///
/// parameters_json:
/// {
///   "tenantId": "d290f1ee-6c54-4b01-90e6-d701748f0851"   // 必填，Tenant.TenantId（GUID 业务键）
/// }
///
/// admin-service 的 base URL 与共享密钥从环境变量读取，见 InternalServiceClientHelper。
/// </summary>
[TaskPlugin("admin-activate-tenant",
    Description = "调用 admin-service 把租户状态置为 Active",
    Version = "1.0.0")]
public class ActivateTenantHandler : ITaskHandler
{
    private readonly Func<HttpClient> _clientFactory;

    public ActivateTenantHandler()
        : this(() => InternalServiceClientHelper.CreateClient(MySqlPluginHelper.AdminServiceBaseUrlEnvVar)) { }

    /// <summary>测试注入点：替换 HttpClient 来源。</summary>
    internal ActivateTenantHandler(Func<HttpClient> clientFactory)
    {
        _clientFactory = clientFactory;
    }

    private sealed record Parameters(string? TenantId);

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

        if (string.IsNullOrWhiteSpace(parameters?.TenantId))
        {
            return TaskResult.Fail("缺少必填参数 tenantId");
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
                var response = await client.PutAsync(
                    $"/internal/tenants/{parameters.TenantId}/activate",
                    content: null,
                    cancellationToken);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return TaskResult.Fail($"admin-service: tenant {parameters.TenantId} not found");
                }

                response.EnsureSuccessStatusCode();
                return TaskResult.Ok();
            }
        }
        catch (HttpRequestException ex)
        {
            return TaskResult.Fail($"调用 admin-service 失败: {ex.Message}");
        }
    }
}
