using System.ComponentModel;
using ModelContextProtocol.Server;
using SqlServerMcp.Application;
using SqlServerMcp.Contracts;

namespace SqlServerMcp.Tools;

/// <summary>
/// Exposes MCP tools for SQL Server metadata inspection.
/// </summary>
internal sealed class MetadataTools
{
    /// <summary>
    /// Returns basic information about the current SQL Server instance and active database.
    /// </summary>
    [McpServerTool, Description("Returns current SQL Server and database information.")]
    public static Task<JsonToolResponse> GetDatabaseInfo(
        ISqlMetadataService service,
        DatabaseInfoRequest request,
        CancellationToken cancellationToken) =>
        ToolExecution.ExecuteAsync(
            "get_database_info",
            async ct =>
            {
                var (info, target) = await service.GetDatabaseInfoAsync(request, ct).ConfigureAwait(false);
                return (info, target, null);
            },
            cancellationToken);

    /// <summary>
    /// Lists the schemas available in the current database.
    /// </summary>
    [McpServerTool, Description("Lists database schemas.")]
    public static Task<JsonToolResponse> ListSchemas(
        ISqlMetadataService service,
        ListSchemasRequest request,
        CancellationToken cancellationToken) =>
        ToolExecution.ExecuteAsync(
            "list_schemas",
            async ct =>
            {
                var (schemas, target, paging) = await service.ListSchemasAsync(request, ct).ConfigureAwait(false);
                return (schemas, target, paging);
            },
            cancellationToken);

    /// <summary>
    /// Lists tables in the current database and can optionally include views and system objects.
    /// </summary>
    [McpServerTool, Description("Lists tables and optionally views.")]
    public static Task<JsonToolResponse> ListTables(
        ISqlMetadataService service,
        ListTablesRequest request,
        CancellationToken cancellationToken) =>
        ToolExecution.ExecuteAsync(
            "list_tables",
            async ct =>
            {
                var (tables, target, paging) = await service.ListTablesAsync(request, ct).ConfigureAwait(false);
                return (tables, target, paging);
            },
            cancellationToken);

    /// <summary>
    /// Returns detailed metadata for a specific table, including columns, indexes, and foreign keys.
    /// </summary>
    [McpServerTool, Description("Returns table details including columns, indexes, and foreign keys.")]
    public static Task<JsonToolResponse> GetTableDetails(
        ISqlMetadataService service,
        TableDetailsRequest request,
        CancellationToken cancellationToken) =>
        ToolExecution.ExecuteAsync(
            "get_table_details",
            async ct =>
            {
                var (details, target) = await service.GetTableDetailsAsync(request, ct).ConfigureAwait(false);
                return (details, target, null);
            },
            cancellationToken);

    /// <summary>
    /// Returns the stored SQL definition of an object when SQL Server exposes one.
    /// </summary>
    [McpServerTool, Description("Returns the SQL definition for an object when SQL Server stores one.")]
    public static Task<JsonToolResponse> GetObjectDefinition(
        ISqlMetadataService service,
        ObjectDefinitionRequest request,
        CancellationToken cancellationToken) =>
        ToolExecution.ExecuteAsync(
            "get_object_definition",
            async ct =>
            {
                var (definition, target) = await service.GetObjectDefinitionAsync(request, ct).ConfigureAwait(false);
                return (definition, target, null);
            },
            cancellationToken);
}
