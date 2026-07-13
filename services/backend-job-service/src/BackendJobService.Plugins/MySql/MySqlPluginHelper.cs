using System.Text.Json;
using System.Text.RegularExpressions;

namespace BackendJobService.Plugins.MySql;

internal static partial class MySqlPluginHelper
{
    /// <summary>
    /// 管理员连接串环境变量。CREATE DATABASE / CREATE USER 需要管理员权限，凭证不允许
    /// 出现在 job_tasks.parameters_json 里落库，统一由宿主部署环境注入。
    /// </summary>
    public const string AdminDsnEnvVar = "JOB_PLUGIN_MYSQL_ADMIN_DSN";

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

    public static string? GetAdminDsnFromEnvironment() =>
        Environment.GetEnvironmentVariable(AdminDsnEnvVar);
}
