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
        string dbName,
        string dbUsername,
        string dbPassword,
        CancellationToken cancellationToken)
    {
        var jobRequest = new CreateJobRequest(
            Name: $"provision-tenant-db-{dbName}",
            Description: "审核开户：创建租户数据库并授权",
            ScheduleType: JobScheduleType.OneTime,
            RunAt: DateTime.UtcNow);

        var jobResponse = await httpClient.PostAsJsonAsync("/backend-job-service/api/v1/jobs", jobRequest, cancellationToken);
        jobResponse.EnsureSuccessStatusCode();
        var job = await jobResponse.Content.ReadFromJsonAsync<JobResponse>(cancellationToken)
            ?? throw new InvalidOperationException("backend-job-service returned an empty response body");

        var createDatabaseTask = new CreateJobTaskRequest(
            Name: "create-database",
            Order: 1,
            HandlerType: "BackendJobService.Plugins.MySql.CreateDatabaseHandler",
            PluginAssembly: PluginAssembly,
            ParametersJson: JsonSerializer.Serialize(new { databaseName = dbName }),
            TimeoutSeconds: 300,
            MaxRetryCount: 2);
        var createDatabaseResponse = await httpClient.PostAsJsonAsync(
            $"/backend-job-service/api/v1/jobs/{job.Id}/tasks", createDatabaseTask, cancellationToken);
        createDatabaseResponse.EnsureSuccessStatusCode();

        var createUserTask = new CreateJobTaskRequest(
            Name: "create-user",
            Order: 2,
            HandlerType: "BackendJobService.Plugins.MySql.CreateUserHandler",
            PluginAssembly: PluginAssembly,
            ParametersJson: JsonSerializer.Serialize(new
            {
                username = dbUsername,
                password = dbPassword,
                grantDatabase = dbName,
                privileges = new[] { "ALL PRIVILEGES" },
            }),
            TimeoutSeconds: 300,
            MaxRetryCount: 2);
        var createUserResponse = await httpClient.PostAsJsonAsync(
            $"/backend-job-service/api/v1/jobs/{job.Id}/tasks", createUserTask, cancellationToken);
        createUserResponse.EnsureSuccessStatusCode();

        return job.Id;
    }
}
