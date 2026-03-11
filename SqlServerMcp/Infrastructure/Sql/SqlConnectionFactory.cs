using Microsoft.Data.SqlClient;

namespace SqlServerMcp.Infrastructure.Sql;

public interface ISqlConnectionFactory
{
    Task<SqlConnection> OpenAsync(string connectionString, int? commandTimeoutSeconds, CancellationToken cancellationToken);
}

public sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    public async Task<SqlConnection> OpenAsync(string connectionString, int? commandTimeoutSeconds, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("ConnectionString is required.", nameof(connectionString));
        }

        var builder = new SqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.ApplicationName))
        {
            builder.ApplicationName = "SqlServerMcp";
        }

        var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }
}
