using System.ComponentModel;
using ModelContextProtocol.Server;
using SqlServerMcp.Application;
using SqlServerMcp.Contracts;

namespace SqlServerMcp.Tools;

/// <summary>
/// Exposes MCP tools for read queries and guarded data changes.
/// </summary>
internal sealed class DataTools
{
    /// <summary>
    /// Executes a single read-only SQL query and returns the results as JSON rows.
    /// </summary>
    [McpServerTool, Description("Executes a single SELECT or WITH statement and returns JSON rows.")]
    public static Task<JsonToolResponse> QuerySql(
        ISqlQueryService service,
        QuerySqlRequest request,
        CancellationToken cancellationToken) =>
        ToolExecution.ExecuteAsync(
            "query_sql",
            async ct =>
            {
                var (result, target, paging) = await service.ExecuteQueryAsync(request, ct).ConfigureAwait(false);
                return (result, target, paging);
            },
            cancellationToken);

    /// <summary>
    /// Previews the effect of a supported UPDATE or DELETE statement before execution.
    /// </summary>
    [McpServerTool, Description("Previews a simple UPDATE or DELETE by returning affected row count and a JSON sample.")]
    public static Task<JsonToolResponse> PreviewDataChange(
        ISqlMutationService service,
        PreviewDataChangeRequest request,
        CancellationToken cancellationToken) =>
        ToolExecution.ExecuteAsync(
            "preview_data_change",
            async ct =>
            {
                var (result, target) = await service.PreviewAsync(request, ct).ConfigureAwait(false);
                return (result, target, null);
            },
            cancellationToken);

    /// <summary>
    /// Executes a previously previewed UPDATE or DELETE statement.
    /// </summary>
    [McpServerTool, Description("Executes a previously previewed UPDATE or DELETE.")]
    public static Task<JsonToolResponse> ExecuteDataChange(
        ISqlMutationService service,
        ExecuteDataChangeRequest request,
        CancellationToken cancellationToken) =>
        ToolExecution.ExecuteAsync(
            "execute_data_change",
            async ct =>
            {
                var (result, target) = await service.ExecuteAsync(request, ct).ConfigureAwait(false);
                return (result, target, null);
            },
            cancellationToken);
}
