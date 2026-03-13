using Microsoft.Data.SqlClient;
using SqlServerMcp.Contracts;
using SqlServerMcp.Infrastructure.Sql;
using SqlServerMcp.Validation;

namespace SqlServerMcp.Application;

/// <summary>
/// Defines read-only query operations exposed by the MCP tools.
/// </summary>
public interface ISqlQueryService
{
    /// <summary>
    /// Executes a single read-only SQL statement and returns the results as JSON rows.
    /// </summary>
    Task<(QueryResult Result, TargetInfo Target, PagingInfo Paging)> ExecuteQueryAsync(QuerySqlRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Implements read-only query operations for the MCP tools.
/// </summary>
public sealed class SqlQueryService(ISqlConnectionFactory connectionFactory, ISqlScriptAnalyzer scriptAnalyzer) : ISqlQueryService
{
    /// <inheritdoc />
    public async Task<(QueryResult Result, TargetInfo Target, PagingInfo Paging)> ExecuteQueryAsync(QuerySqlRequest request, CancellationToken cancellationToken)
    {
        scriptAnalyzer.AnalyzeSelect(request.Sql);
        var pagination = PaginationParser.Parse(request.Page, request.PageSize);

        await using var connection = await connectionFactory.OpenAsync(request.ConnectionString, request.CommandTimeoutSeconds, cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(request.Sql, connection);
        if (request.CommandTimeoutSeconds is int timeout && timeout > 0)
        {
            command.CommandTimeout = timeout;
        }

        SqlDataMapper.AddParameters(command, request.Parameters);

        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var columns = SqlDataMapper.ReadColumns(reader);
        var rows = await SqlDataMapper.ReadRowsAsync(reader, pagination, cancellationToken).ConfigureAwait(false);
        return (
            new QueryResult(columns, rows),
            new TargetInfo(connection.DataSource, connection.Database),
            new PagingInfo(pagination.Page, pagination.PageSizeLabel, rows.Count, pagination.IsUnbounded));
    }
}
