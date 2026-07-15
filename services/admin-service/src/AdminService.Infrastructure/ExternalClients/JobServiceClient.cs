using System.Net.Http.Json;
using System.Text.Json;
using AdminService.Application.Interfaces;

namespace AdminService.Infrastructure.ExternalClients;

/// <summary>
/// backend-job-service 的 JobScheduleType 枚举（1=Cron, 2=OneTime）。本地镜像一份，
/// 避免跨服务直接引用其 Domain 程序集（违反规范第 2.2/10 章服务自治约束）。
/// System.Text.Json 默认把枚举序列化成数字，与 job-service 侧的默认行为一致。
/// </summary>
internal enum JobScheduleType
{
    Cron = 1,
    OneTime = 2,
}

public class JobServiceClient(HttpClient httpClient) : IJobServiceClient
{
    private const string PluginAssembly = "BackendJobService.Plugins.dll";

    private sealed record CreateJobRequest(string Name, string Description, JobScheduleType ScheduleType, DateTime RunAt);

    private sealed record CreateJobTaskRequest(
        string Name,
        int Order,
        string HandlerType,
        string PluginAssembly,
        string ParametersJson,
        int TimeoutSeconds,
        int MaxRetryCount);

    private sealed record JobResponse(long Id);

    public async Task<long> CreateTenantProvisioningJobAsync(
        long databaseInstanceId,
        string dbName,
        string dbUsername,
        string dbPassword,
        ulong userId,
        ulong reviewedBy,
        string tenantId,
        CancellationToken cancellationToken)
    {
        var jobRequest = new CreateJobRequest(
            Name: $"provision-tenant-db-{dbName}",
            Description: "审核开户：创建租户数据库、建用户授权、标记已审核、激活租户",
            ScheduleType: JobScheduleType.OneTime,
            RunAt: DateTime.UtcNow.AddSeconds(30));

        var jobResponse = await httpClient.PostAsJsonAsync("/backend-job-service/api/v1/jobs", jobRequest, cancellationToken);
        jobResponse.EnsureSuccessStatusCode();
        var job = await jobResponse.Content.ReadFromJsonAsync<JobResponse>(cancellationToken)
            ?? throw new InvalidOperationException("backend-job-service returned an empty response body");

        var createDatabaseTask = new CreateJobTaskRequest(
            Name: "create-database",
            Order: 1,
            HandlerType: "BackendJobService.Plugins.MySql.CreateDatabaseHandler",
            PluginAssembly: PluginAssembly,
            ParametersJson: JsonSerializer.Serialize(new { databaseInstanceId, databaseName = dbName }),
            TimeoutSeconds: 300,
            MaxRetryCount: 2);
        await CreateTaskAsync(job.Id, createDatabaseTask, cancellationToken);

        var createUserTask = new CreateJobTaskRequest(
            Name: "create-user",
            Order: 2,
            HandlerType: "BackendJobService.Plugins.MySql.CreateUserHandler",
            PluginAssembly: PluginAssembly,
            ParametersJson: JsonSerializer.Serialize(new
            {
                databaseInstanceId,
                username = dbUsername,
                password = dbPassword,
                grantDatabase = dbName,
                privileges = new[] { "ALL PRIVILEGES" },
            }),
            TimeoutSeconds: 300,
            MaxRetryCount: 2);
        await CreateTaskAsync(job.Id, createUserTask, cancellationToken);

        var markUserReviewedTask = new CreateJobTaskRequest(
            Name: "mark-user-reviewed",
            Order: 3,
            HandlerType: "BackendJobService.Plugins.Sso.MarkUserReviewedHandler",
            PluginAssembly: PluginAssembly,
            ParametersJson: JsonSerializer.Serialize(new { userId, reviewedBy }),
            TimeoutSeconds: 60,
            MaxRetryCount: 2);
        await CreateTaskAsync(job.Id, markUserReviewedTask, cancellationToken);

        var activateTenantTask = new CreateJobTaskRequest(
            Name: "activate-tenant",
            Order: 4,
            HandlerType: "BackendJobService.Plugins.Admin.ActivateTenantHandler",
            PluginAssembly: PluginAssembly,
            ParametersJson: JsonSerializer.Serialize(new { tenantId }),
            TimeoutSeconds: 60,
            MaxRetryCount: 2);
        await CreateTaskAsync(job.Id, activateTenantTask, cancellationToken);

        return job.Id;
    }

    private async Task CreateTaskAsync(long jobId, CreateJobTaskRequest task, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"/backend-job-service/api/v1/jobs/{jobId}/tasks", task, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
