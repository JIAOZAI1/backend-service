using BackendJobService.Contracts;
using BackendJobService.Plugins.Internal;
using BackendJobService.Plugins.MySql;

namespace BackendJobService.Plugins.Admin;

/// <summary>
/// 调用 admin-service 的 PUT /internal/tenants/expire-overdue，批量检查所有 License 已过期的
/// Active 租户并置为 Expired。admin-service 侧幂等（已 Expired 的租户不在查询范围内，不会重复
/// 处理），本插件不额外做幂等短路。不针对单个租户，不需要入参，parameters_json 恒为 "{}"。
///
/// 供每日全局 Cron Job 使用（见 backend-job-service README"内置插件"一节），只挂这一个 Task，
/// 由 backend-job-service 的调度器按 CronExpression 自动重复触发，不需要每天重建 Job。
///
/// admin-service 的 base URL 与共享密钥从环境变量读取，见 InternalServiceClientHelper。
/// </summary>
[TaskPlugin("admin-expire-overdue-tenants",
    Description = "调用 admin-service 批量检查并过期到期租户",
    Version = "1.0.0")]
public class ExpireOverdueTenantsHandler : ITaskHandler
{
    private readonly Func<HttpClient> _clientFactory;

    public ExpireOverdueTenantsHandler()
        : this(() => InternalServiceClientHelper.CreateClient(MySqlPluginHelper.AdminServiceBaseUrlEnvVar)) { }

    /// <summary>测试注入点：替换 HttpClient 来源。</summary>
    internal ExpireOverdueTenantsHandler(Func<HttpClient> clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<TaskResult> ExecuteAsync(TaskExecutionContext context, CancellationToken cancellationToken)
    {
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
                    "/internal/tenants/expire-overdue",
                    content: null,
                    cancellationToken);

                response.EnsureSuccessStatusCode();

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                return TaskResult.Ok(body);
            }
        }
        catch (HttpRequestException ex)
        {
            return TaskResult.Fail($"调用 admin-service 失败: {ex.Message}");
        }
    }
}
