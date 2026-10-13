using Npgsql;

namespace FixFlow.API.Configuration;

internal static class ConnectionStrings
{
    /// <summary>
    /// The PostgreSQL connection string: host, database and user from <c>ConnectionStrings:FixFlow</c>, the password
    /// only from <c>Database:Password</c> (user-secrets locally, an environment variable in containers).
    /// </summary>
    public static string GetPostgresConnectionString(this IConfiguration configuration)
    {
        var password = configuration["Database:Password"];
        if (string.IsNullOrEmpty(password))
        {
            throw new InvalidOperationException(
                "Database:Password is not set. Locally: dotnet user-secrets set \"Database:Password\" " +
                "\"<FIXFLOW_DB_PASSWORD from .env>\" --project src/FixFlow.API");
        }

        return new NpgsqlConnectionStringBuilder(configuration.GetRequiredConnectionString("FixFlow"))
        {
            Password = password,
        }.ConnectionString;
    }

    public static string GetRequiredConnectionString(this IConfiguration configuration, string name)
    {
        var connectionString = configuration.GetConnectionString(name);
        return string.IsNullOrWhiteSpace(connectionString)
            ? throw new InvalidOperationException($"ConnectionStrings:{name} is not set.")
            : connectionString;
    }
}
