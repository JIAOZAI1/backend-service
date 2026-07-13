using System.Text.Json;
using BackendJobService.Contracts;
using MySqlConnector;

namespace BackendJobService.Plugins.MySql;

/// <summary>
/// 在目标 MySQL 实例上创建数据库。幂等：库已存在则跳过，通过 output 的 alreadyExisted 区分。
///
/// parameters_json:
/// {
///   "databaseName": "example_db",        // 必填，仅字母数字下划线
///   "charset": "utf8mb4",                // 可选，默认 utf8mb4
///   "collation": "utf8mb4_general_ci"    // 可选，缺省用 charset 的默认排序规则
/// }
///
/// 管理员连接串从环境变量 JOB_PLUGIN_MYSQL_ADMIN_DSN 读取（MySqlConnector 连接串格式），
/// 不通过任务参数传递，避免凭证落库。
/// </summary>
[TaskPlugin("mysql-create-database",
    Description = "在目标 MySQL 实例上创建数据库（幂等，已存在则跳过）",
    Version = "1.0.0")]
public class CreateDatabaseHandler : ITaskHandler
{
    private readonly Func<string?> _adminDsnProvider;

    public CreateDatabaseHandler() : this(MySqlPluginHelper.GetAdminDsnFromEnvironment) { }

    /// <summary>测试注入点：替换管理员连接串来源。</summary>
    internal CreateDatabaseHandler(Func<string?> adminDsnProvider)
    {
        _adminDsnProvider = adminDsnProvider;
    }

    private sealed record Parameters(string? DatabaseName, string? Charset, string? Collation);

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

        if (string.IsNullOrWhiteSpace(parameters?.DatabaseName))
        {
            return TaskResult.Fail("缺少必填参数 databaseName");
        }

        if (!MySqlPluginHelper.IsValidIdentifier(parameters.DatabaseName))
        {
            return TaskResult.Fail($"databaseName '{parameters.DatabaseName}' 非法：仅允许字母、数字、下划线，长度 1-64");
        }

        var charset = string.IsNullOrWhiteSpace(parameters.Charset) ? "utf8mb4" : parameters.Charset;
        if (!MySqlPluginHelper.IsValidIdentifier(charset))
        {
            return TaskResult.Fail($"charset '{charset}' 非法");
        }

        if (parameters.Collation is not null && !MySqlPluginHelper.IsValidIdentifier(parameters.Collation))
        {
            return TaskResult.Fail($"collation '{parameters.Collation}' 非法");
        }

        var adminDsn = _adminDsnProvider();
        if (string.IsNullOrWhiteSpace(adminDsn))
        {
            return TaskResult.Fail($"未配置环境变量 {MySqlPluginHelper.AdminDsnEnvVar}（MySQL 管理员连接串）");
        }

        try
        {
            await using var connection = new MySqlConnection(adminDsn);
            await connection.OpenAsync(cancellationToken);

            bool alreadyExisted;
            await using (var check = connection.CreateCommand())
            {
                check.CommandText = "SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME = @name";
                check.Parameters.AddWithValue("@name", parameters.DatabaseName);
                alreadyExisted = Convert.ToInt64(await check.ExecuteScalarAsync(cancellationToken)) > 0;
            }

            var sql = $"CREATE DATABASE IF NOT EXISTS {MySqlPluginHelper.QuoteIdentifier(parameters.DatabaseName)} CHARACTER SET {charset}";
            if (parameters.Collation is not null)
            {
                sql += $" COLLATE {parameters.Collation}";
            }

            await using (var create = connection.CreateCommand())
            {
                create.CommandText = sql;
                await create.ExecuteNonQueryAsync(cancellationToken);
            }

            var output = JsonSerializer.Serialize(new
            {
                database = parameters.DatabaseName,
                charset,
                collation = parameters.Collation,
                alreadyExisted,
            });
            return TaskResult.Ok(output);
        }
        catch (MySqlException ex)
        {
            return TaskResult.Fail($"MySQL 执行失败: {ex.Message}");
        }
    }
}
