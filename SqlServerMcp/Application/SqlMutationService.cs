using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SqlServerMcp.Configuration;
using SqlServerMcp.Contracts;
using SqlServerMcp.Infrastructure.Sql;
using SqlServerMcp.Validation;

namespace SqlServerMcp.Application;

/// <summary>
/// Defines guarded data change operations exposed by the MCP tools.
/// </summary>
public interface ISqlMutationService
{
    /// <summary>
    /// Previews the effect of a supported UPDATE or DELETE statement.
    /// </summary>
    Task<(MutationPreviewResult Result, TargetInfo Target)> PreviewAsync(PreviewDataChangeRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Executes a previously previewed UPDATE or DELETE statement.
    /// </summary>
    Task<(MutationExecutionResult Result, TargetInfo Target)> ExecuteAsync(ExecuteDataChangeRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Implements guarded data change operations for the MCP tools.
/// </summary>
public sealed class SqlMutationService(
    ISqlConnectionFactory connectionFactory,
    ISqlScriptAnalyzer scriptAnalyzer,
    IPreviewTokenStore previewTokenStore,
    IOptions<SqlServerMcpOptions> options) : ISqlMutationService
{
    /// <inheritdoc />
    public async Task<(MutationPreviewResult Result, TargetInfo Target)> PreviewAsync(PreviewDataChangeRequest request, CancellationToken cancellationToken)
    {
        var analysis = scriptAnalyzer.AnalyzeDataChange(request.Sql);
        EnsureAffectAllRowsAllowed(request.AllowAffectAllRows, analysis);

        await using var connection = await connectionFactory.OpenAsync(request.ConnectionString, request.CommandTimeoutSeconds, cancellationToken).ConfigureAwait(false);

        var previewSql = BuildPreviewSql(analysis);
        await using var command = new SqlCommand(previewSql, connection);
        if (request.CommandTimeoutSeconds is int timeout && timeout > 0)
        {
            command.CommandTimeout = timeout;
        }

        command.Parameters.AddWithValue("@sampleLimit", options.Value.PreviewSampleLimit);
        SqlDataMapper.AddParameters(command, request.Parameters);

        long affectedRows;
        var sampleRows = new List<Dictionary<string, object?>>();

        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        affectedRows = reader.GetInt64(0);

        if (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                sampleRows.Add(SqlDataMapper.ReadCurrentRow(reader));
            }
        }

        var token = previewTokenStore.Create(request.ConnectionString, analysis.NormalizedSql, request.Parameters, request.AllowAffectAllRows);
        return (
            new MutationPreviewResult(affectedRows, sampleRows, token, !options.Value.SkipDmlConfirmation, request.AllowAffectAllRows),
            new TargetInfo(connection.DataSource, connection.Database));
    }

    /// <inheritdoc />
    public async Task<(MutationExecutionResult Result, TargetInfo Target)> ExecuteAsync(ExecuteDataChangeRequest request, CancellationToken cancellationToken)
    {
        var analysis = scriptAnalyzer.AnalyzeDataChange(request.Sql);
        EnsureAffectAllRowsAllowed(request.AllowAffectAllRows, analysis);

        var skipConfirmation = options.Value.SkipDmlConfirmation;
        if (!skipConfirmation &&
            !previewTokenStore.Validate(request.PreviewToken, request.ConnectionString, analysis.NormalizedSql, request.Parameters, request.AllowAffectAllRows))
        {
            throw new InvalidOperationException("The preview token is missing, expired, or does not match the current statement.");
        }

        await using var connection = await connectionFactory.OpenAsync(request.ConnectionString, request.CommandTimeoutSeconds, cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(request.Sql, connection);
        if (request.CommandTimeoutSeconds is int timeout && timeout > 0)
        {
            command.CommandTimeout = timeout;
        }

        SqlDataMapper.AddParameters(command, request.Parameters);
        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        return (new MutationExecutionResult(rowsAffected, skipConfirmation), new TargetInfo(connection.DataSource, connection.Database));
    }

    private static string BuildPreviewSql(SqlScriptAnalysis analysis)
    {
        var target = SqlIdentifier.Qualify(analysis.TargetSchema!, analysis.TargetTable!);
        var whereClause = analysis.HasWhereClause ? $" WHERE {analysis.WhereClause}" : string.Empty;

        return $"""
            SELECT COUNT_BIG(1) AS AffectedRows
            FROM {target}{whereClause};

            SELECT TOP (@sampleLimit) *
            FROM {target}{whereClause};
            """;
    }

    private static void EnsureAffectAllRowsAllowed(bool allowAffectAllRows, SqlScriptAnalysis analysis)
    {
        if (!analysis.HasWhereClause && !allowAffectAllRows)
        {
            throw new InvalidOperationException("A WHERE clause is required unless AllowAffectAllRows is true.");
        }
    }
}
