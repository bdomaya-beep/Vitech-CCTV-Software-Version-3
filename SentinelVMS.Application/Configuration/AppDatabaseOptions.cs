namespace SentinelVMS.Application.Configuration;

public sealed class AppDatabaseOptions
{
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.Sqlite;
    public string ConnectionString { get; set; } = "Data Source=sentinel-vms.db";
}
