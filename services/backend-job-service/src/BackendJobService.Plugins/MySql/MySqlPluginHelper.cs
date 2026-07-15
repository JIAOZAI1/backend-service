using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using BackendJobService.Plugins.Internal;

namespace BackendJobService.Plugins.MySql;

internal static partial class MySqlPluginHelper
{
    /// <summary>admin-service 的 base URL，供现取 DatabaseInstance 凭据用（见 ResolveAdminDsnAsync）。</summary>
    public const string AdminServiceBaseUrlEnvVar = "JOB_PLUGIN_ADMIN_SERVICE_BASE_URL";

    /// <summary>parameters_json 反序列化选项：camelCase + 大小写不敏感。</summary>
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [GeneratedRegex("^[A-Za-z0-9_]{1,64}$")]
    private static partial Regex IdentifierPattern();

    [GeneratedRegex("^[A-Za-z0-9_]{1,32}$")]
    private static partial Regex UserNamePattern();

    [GeneratedRegex("^[A-Za-z0-9_.%-]{1,255}$")]
    private static partial Regex HostPattern();

    /// <summary>GRANT 语句允许的权限白名单（数据库级）。标识符无法参数化，必须白名单校验。</summary>
    private static readonly HashSet<string> AllowedPrivileges = new(StringComparer.OrdinalIgnoreCase)
    {
        "ALL", "ALL PRIVILEGES",
        "SELECT", "INSERT", "UPDATE", "DELETE",
        "CREATE", "DROP", "INDEX", "ALTER", "REFERENCES",
        "EXECUTE", "CREATE VIEW", "SHOW VIEW",
        "CREATE ROUTINE", "ALTER ROUTINE", "EVENT", "TRIGGER",
        "LOCK TABLES", "CREATE TEMPORARY TABLES",
    };

    /// <summary>库名/字符集/排序规则等标识符：仅字母数字下划线，防注入。</summary>
    public static bool IsValidIdentifier(string value) => IdentifierPattern().IsMatch(value);

    public static bool IsValidUserName(string value) => UserNamePattern().IsMatch(value);

    public static bool IsValidHost(string value) => HostPattern().IsMatch(value);

    public static bool IsAllowedPrivilege(string value) => AllowedPrivileges.Contains(value.Trim());

    /// <summary>反引号包裹标识符。仅用于已通过 IsValidIdentifier 校验的值。</summary>
    public static string QuoteIdentifier(string identifier) => $"`{identifier}`";

    /// <summary>转义为 MySQL 单引号字符串字面量内容（DDL 中密码无法参数化）。</summary>
    public static string EscapeStringLiteral(string value) =>
        value.Replace("\\", "\\\\").Replace("'", "''");

    public readonly record struct AdminDsnResult(string? Dsn, string? Error)
    {
        public static AdminDsnResult Ok(string dsn) => new(dsn, null);
        public static AdminDsnResult Fail(string error) => new(null, error);
    }

    private sealed record CredentialsResponse(string DbType, string Host, int Port, string Username, string Password);

    /// <summary>
    /// 按 databaseInstanceId 调 admin-service 的 /internal/database-instances/{id}/credentials
    /// 现取解密后的连接信息，拼成 MySqlConnector DSN。不缓存、不落库，每次任务执行现取现用。
    /// </summary>
    public static async Task<AdminDsnResult> ResolveAdminDsnAsync(
        Func<HttpClient> adminServiceClientFactory, long databaseInstanceId, CancellationToken cancellationToken)
    {
        HttpClient client;
        try
        {
            client = adminServiceClientFactory();
        }
        catch (InvalidOperationException ex)
        {
            return AdminDsnResult.Fail(ex.Message);
        }

        try
        {
            using (client)
            {
                var response = await client.GetAsync(
                    $"/internal/database-instances/{databaseInstanceId}/credentials", cancellationToken);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return AdminDsnResult.Fail($"database instance {databaseInstanceId} not found");
                }

                response.EnsureSuccessStatusCode();

                var credentials = await response.Content.ReadFromJsonAsync<CredentialsResponse>(cancellationToken)
                    ?? throw new InvalidOperationException("admin-service returned an empty response body");

                var dsn = $"Server={credentials.Host};Port={credentials.Port};User ID={credentials.Username};Password={credentials.Password};";
                return AdminDsnResult.Ok(dsn);
            }
        }
        catch (HttpRequestException ex)
        {
            return AdminDsnResult.Fail($"调用 admin-service 获取数据库实例凭据失败: {ex.Message}");
        }
    }
}
