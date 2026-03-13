using Microsoft.Data.SqlClient;

namespace SqlServerMcp.Infrastructure.Sql;

/// <summary>
/// Opens SQL Server connections using the connection string supplied by each request.
/// </summary>
public interface ISqlConnectionFactory
{
    /// <summary>
    /// Opens and returns a SQL Server connection.
    /// </summary>
    Task<SqlConnection> OpenAsync(string connectionString, int? commandTimeoutSeconds, CancellationToken cancellationToken);
}

/// <summary>
/// Default implementation of <see cref="ISqlConnectionFactory"/>.
/// </summary>
public sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    /// <inheritdoc />
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
