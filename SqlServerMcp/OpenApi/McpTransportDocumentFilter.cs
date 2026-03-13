using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using SqlServerMcp.Configuration;
using Swashbuckle.AspNetCore.SwaggerGen;
using HttpMethod = System.Net.Http.HttpMethod;

namespace SqlServerMcp.OpenApi;

/// <summary>
/// Adds OpenAPI entries that describe the MCP Streamable HTTP transport exposed by this server.
/// </summary>
internal sealed class McpTransportDocumentFilter(IOptions<SqlServerMcpOptions> options) : IDocumentFilter
{
    private readonly SqlServerMcpOptions _options = options.Value;

    /// <inheritdoc />
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var path = NormalizePath(_options.HttpPath);
        swaggerDoc.Paths[path] = CreateMcpPathItem();
        swaggerDoc.Paths[$"{path}/sse"] = CreateLegacySsePathItem();
        swaggerDoc.Paths[$"{path}/message"] = CreateLegacyMessagePathItem();
    }

    private OpenApiPathItem CreateMcpPathItem()
    {
        var item = new OpenApiPathItem
        {
            Description = "Primary MCP Streamable HTTP endpoint."
        };

        item.Operations ??= [];
        item.Operations[HttpMethod.Post] = new OpenApiOperation
        {
            Summary = "Send MCP JSON-RPC requests",
            Description = """
                Sends JSON-RPC requests to the MCP server over the Streamable HTTP transport.

                Typical flow:

                1. Send an `initialize` request to start a session.
                2. Call `tools/list` to discover the available tools.
                3. Call `tools/call` with arguments such as `connectionString`, `schema`, or `sql`.

                Example `tools/call` request for `list_tables`:

                ```json
                {
                  "jsonrpc": "2.0",
                  "id": 2,
                  "method": "tools/call",
                  "params": {
                    "name": "list_tables",
                    "arguments": {
                      "connectionString": "Server=tcp:sql-demo.example.net,1433;Database=SalesDb;User ID=app_reader;Password=ReplaceWithStrongPassword123!;Encrypt=True;TrustServerCertificate=False;",
                      "includeViews": false,
                      "includeSystemObjects": false,
                      "page": 1,
                      "pageSize": "25"
                    }
                  }
                }
                ```

                Example result payload returned by the tool:

                ```json
                {
                  "ok": true,
                  "operation": "list_tables",
                  "data": [
                    {
                      "schema": "dbo",
                      "name": "Customers",
                      "objectType": "USER_TABLE",
                      "approximateRowCount": 1204
                    }
                  ],
                  "target": {
                    "server": "sql-demo.example.net",
                    "database": "SalesDb"
                  },
                  "paging": {
                    "page": 1,
                    "pageSize": "25",
                    "returnedRows": 1,
                    "isUnbounded": false
                  },
                  "metrics": {
                    "durationMs": 18
                  }
                }
                ```
                """,
            Parameters =
            [
                CreateHeaderParameter("MCP-Protocol-Version", "Optional protocol version header used by MCP clients."),
                CreateHeaderParameter("MCP-Session-Id", "Session identifier returned by the server after initialization."),
                CreateHeaderParameter("Accept", "Use `application/json, text/event-stream` when the client can process streamed responses.")
            ],
            RequestBody = new OpenApiRequestBody
            {
                Required = true,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new()
                    {
                        Schema = CreateGenericJsonSchema()
                    }
                }
            },
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse { Description = "JSON-RPC response or an SSE stream depending on the request and Accept headers." },
                ["400"] = new OpenApiResponse { Description = "Invalid MCP request, missing session header, or unsupported protocol version." },
                ["404"] = new OpenApiResponse { Description = "Unknown or expired session." }
            }
        };

        item.Operations[HttpMethod.Get] = new OpenApiOperation
        {
            Summary = "Open the optional SSE stream",
            Description = """
                Opens the optional Streamable HTTP GET channel for server-to-client messages.
                Keep the request open to receive events such as notifications or related protocol messages.
                """,
            Parameters =
            [
                CreateHeaderParameter("MCP-Session-Id", "Session identifier returned by the server after initialization.")
            ],
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse { Description = "SSE stream with MCP messages." },
                ["400"] = new OpenApiResponse { Description = "Session header required for stateful requests after initialization." },
                ["404"] = new OpenApiResponse { Description = "Unknown or expired session." }
            }
        };

        item.Operations[HttpMethod.Delete] = new OpenApiOperation
        {
            Summary = "Terminate an MCP session",
            Description = "Explicitly closes an MCP HTTP session using the `MCP-Session-Id` header.",
            Parameters =
            [
                CreateHeaderParameter("MCP-Session-Id", "Session identifier to terminate.")
            ],
            Responses = new OpenApiResponses
            {
                ["204"] = new OpenApiResponse { Description = "Session terminated." },
                ["400"] = new OpenApiResponse { Description = "Missing or invalid session header." },
                ["404"] = new OpenApiResponse { Description = "Unknown or expired session." },
                ["405"] = new OpenApiResponse { Description = "Session termination is not enabled by the server or transport implementation." }
            }
        };

        return item;
    }

    private static OpenApiPathItem CreateLegacySsePathItem()
    {
        var item = new OpenApiPathItem
        {
            Description = "Deprecated HTTP+SSE transport endpoint kept for backwards compatibility."
        };

        item.Operations ??= [];
        item.Operations[HttpMethod.Get] = new OpenApiOperation
        {
            Deprecated = true,
            Summary = "Open deprecated SSE endpoint",
            Description = "Legacy SSE endpoint exposed for older MCP clients. Prefer the main MCP endpoint instead.",
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse { Description = "Legacy SSE stream." }
            }
        };

        return item;
    }

    private static OpenApiPathItem CreateLegacyMessagePathItem()
    {
        var item = new OpenApiPathItem
        {
            Description = "Deprecated POST endpoint used alongside the legacy SSE transport."
        };

        item.Operations ??= [];
        item.Operations[HttpMethod.Post] = new OpenApiOperation
        {
            Deprecated = true,
            Summary = "Send a legacy MCP message",
            Description = "Legacy HTTP+SSE message endpoint kept for backwards compatibility. Prefer the main MCP endpoint instead.",
            RequestBody = new OpenApiRequestBody
            {
                Required = true,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new()
                    {
                        Schema = CreateGenericJsonSchema()
                    }
                }
            },
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse { Description = "Legacy MCP response." }
            }
        };

        return item;
    }

    private static OpenApiParameter CreateHeaderParameter(string name, string description) =>
        new()
        {
            Name = name,
            In = ParameterLocation.Header,
            Required = false,
            Description = description,
            Schema = new OpenApiSchema { Type = JsonSchemaType.String }
        };

    private static OpenApiSchema CreateGenericJsonSchema() =>
        new()
        {
            Type = JsonSchemaType.Object,
            AdditionalPropertiesAllowed = true,
            Description = "Generic JSON object carrying MCP protocol messages."
        };

    private static string NormalizePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "/mcp";
        }

        return value.StartsWith('/') ? value : $"/{value}";
    }
}
