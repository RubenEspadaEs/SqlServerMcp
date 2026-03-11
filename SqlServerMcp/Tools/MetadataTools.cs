using System.ComponentModel;
using ModelContextProtocol.Server;
using SqlServerMcp.Application;
using SqlServerMcp.Contracts;

namespace SqlServerMcp.Tools;

internal sealed class MetadataTools
{
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
