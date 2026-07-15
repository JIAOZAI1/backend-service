using System.Text.Json;
using BackendJobService.Contracts;
using BackendJobService.Plugins.Internal;
using MySqlConnector;

namespace BackendJobService.Plugins.MySql;

/// <summary>
/// 在目标 MySQL 实例上创建用户，并可选对指定数据库授权。幂等：用户已存在则跳过创建
/// （IF NOT EXISTS 语义，不会更新已有用户的密码），授权语句本身可重复执行。
///
/// parameters_json:
/// {
///   "databaseInstanceId": 1,             // 必填，admin-service 已登记的 DatabaseInstance.Id
///   "username": "example_user",          // 必填，仅字母数字下划线，长度 1-32
///   "password": "***",                   // 必填，由调用方生成并自行保管；不会写入 output
///   "host": "%",                         // 可选，默认 "%"
///   "grantDatabase": "example_db",       // 可选，指定后对该库执行 GRANT
///   "privileges": ["SELECT", "INSERT"]   // 可选，默认 ["ALL PRIVILEGES"]，白名单校验
/// }
///
/// 管理员连接串不通过任务参数传递，见 CreateDatabaseHandler 的同一段说明与
/// MySqlPluginHelper.ResolveAdminDsnAsync。
/// </summary>
[TaskPlugin("mysql-create-user",
    Description = "在目标 MySQL 实例上创建用户并可选授权（幂等，已存在则跳过且不改密码）",
    Version = "1.1.0")]
public class CreateUserHandler : ITaskHandler
{
    private readonly Func<HttpClient> _adminServiceClientFactory;

    public CreateUserHandler()
        : this(() => InternalServiceClientHelper.CreateClient(MySqlPluginHelper.AdminServiceBaseUrlEnvVar)) { }

    /// <summary>测试注入点：替换 admin-service HttpClient 来源。</summary>
    internal CreateUserHandler(Func<HttpClient> adminServiceClientFactory)
    {
        _adminServiceClientFactory = adminServiceClientFactory;
    }

    private sealed record Parameters(
        long? DatabaseInstanceId,
        string? Username,
        string? Password,
        string? Host,
        string? GrantDatabase,
        string[]? Privileges);

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

        if (parameters?.DatabaseInstanceId is null)
        {
            return TaskResult.Fail("缺少必填参数 databaseInstanceId");
        }

        if (string.IsNullOrWhiteSpace(parameters.Username))
        {
            return TaskResult.Fail("缺少必填参数 username");
        }

        if (!MySqlPluginHelper.IsValidUserName(parameters.Username))
        {
            return TaskResult.Fail($"username '{parameters.Username}' 非法：仅允许字母、数字、下划线，长度 1-32");
        }

        if (string.IsNullOrEmpty(parameters.Password))
        {
            return TaskResult.Fail("缺少必填参数 password");
        }

        var host = string.IsNullOrWhiteSpace(parameters.Host) ? "%" : parameters.Host;
        if (!MySqlPluginHelper.IsValidHost(host))
        {
            return TaskResult.Fail($"host '{host}' 非法");
        }

        if (parameters.GrantDatabase is not null && !MySqlPluginHelper.IsValidIdentifier(parameters.GrantDatabase))
        {
            return TaskResult.Fail($"grantDatabase '{parameters.GrantDatabase}' 非法：仅允许字母、数字、下划线，长度 1-64");
        }

        var privileges = parameters.Privileges is { Length: > 0 }
            ? parameters.Privileges.Select(p => p.Trim().ToUpperInvariant()).ToArray()
            : ["ALL PRIVILEGES"];

        var illegalPrivilege = privileges.FirstOrDefault(p => !MySqlPluginHelper.IsAllowedPrivilege(p));
        if (illegalPrivilege is not null)
        {
            return TaskResult.Fail($"privileges 含不在白名单内的权限 '{illegalPrivilege}'");
        }

        var dsnResult = await MySqlPluginHelper.ResolveAdminDsnAsync(
            _adminServiceClientFactory, parameters.DatabaseInstanceId.Value, cancellationToken);
        if (dsnResult.Error is not null)
        {
            return TaskResult.Fail(dsnResult.Error);
        }

        try
        {
            await using var connection = new MySqlConnection(dsnResult.Dsn);
            await connection.OpenAsync(cancellationToken);

            bool alreadyExisted;
            await using (var check = connection.CreateCommand())
            {
                check.CommandText = "SELECT COUNT(*) FROM mysql.user WHERE user = @user AND host = @host";
                check.Parameters.AddWithValue("@user", parameters.Username);
                check.Parameters.AddWithValue("@host", host);
                alreadyExisted = Convert.ToInt64(await check.ExecuteScalarAsync(cancellationToken)) > 0;
            }

            // 账号与密码是 DDL 的一部分，无法参数化：username/host 已白名单校验，
            // 密码经 EscapeStringLiteral 转义后以单引号字面量拼接
            var accountLiteral = $"'{MySqlPluginHelper.EscapeStringLiteral(parameters.Username)}'@'{MySqlPluginHelper.EscapeStringLiteral(host)}'";

            await using (var createUser = connection.CreateCommand())
            {
                createUser.CommandText =
                    $"CREATE USER IF NOT EXISTS {accountLiteral} IDENTIFIED BY '{MySqlPluginHelper.EscapeStringLiteral(parameters.Password)}'";
                await createUser.ExecuteNonQueryAsync(cancellationToken);
            }

            string? grantedOn = null;
            if (parameters.GrantDatabase is not null)
            {
                grantedOn = $"{MySqlPluginHelper.QuoteIdentifier(parameters.GrantDatabase)}.*";
                await using var grant = connection.CreateCommand();
                grant.CommandText = $"GRANT {string.Join(", ", privileges)} ON {grantedOn} TO {accountLiteral}";
                await grant.ExecuteNonQueryAsync(cancellationToken);
            }

            // output 不包含密码；alreadyExisted=true 表示未改动已有用户的密码
            var output = JsonSerializer.Serialize(new
            {
                user = parameters.Username,
                host,
                alreadyExisted,
                grantedPrivileges = grantedOn is not null ? privileges : null,
                grantedOn,
            });
            return TaskResult.Ok(output);
        }
        catch (MySqlException ex)
        {
            return TaskResult.Fail($"MySQL 执行失败: {ex.Message}");
        }
    }
}
