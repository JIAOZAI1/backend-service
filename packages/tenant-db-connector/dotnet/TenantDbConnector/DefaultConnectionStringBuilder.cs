using MySqlConnector;

namespace TenantDbConnector;

internal static class DefaultConnectionStringBuilder
{
    public static string Build(TenantDbInfo info, TenantDbConnectorOptions options)
    {
        var builder = new MySqlConnectionStringBuilder
        {
            Server = info.DbHost,
            Port = (uint)info.DbPort,
            Database = info.DbName,
            UserID = info.DbUsername,
            Password = info.DbPassword,
            MaximumPoolSize = options.MaxPoolSize,
            MinimumPoolSize = options.MinPoolSize,
        };
        return builder.ConnectionString;
    }
}
