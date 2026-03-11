# SqlServerMcp

`SqlServerMcp` is a .NET 10 Model Context Protocol (MCP) server for SQL Server administration and querying. It supports both `HTTP` and `stdio` transports and exposes a focused toolset for metadata inspection, read queries, guarded data changes, and selected administrative operations.

## What It Does

- Lists database metadata such as schemas, tables, and object definitions
- Executes read-only SQL through `query_sql`
- Supports guarded `UPDATE` and `DELETE` flows with preview tokens
- Provides typed helpers for common DDL and security operations
- Returns consistent JSON-shaped responses across tools

## MCP Tools

- `get_database_info`
- `list_schemas`
- `list_tables`
- `get_table_details`
- `get_object_definition`
- `query_sql`
- `preview_data_change`
- `execute_data_change`
- `create_table`
- `alter_table`
- `create_login`
- `create_user`
- `grant_role_membership`
- `execute_admin_sql`

## Operational Rules

- Every tool call must include a `connectionString`
- Responses are normalized and include fields such as `ok`, `operation`, `data`, `target`, `paging`, `metrics`, and `error` when applicable
- `query_sql` accepts only a single `SELECT` or `WITH` statement
- `preview_data_change` and `execute_data_change` accept only simple single-target `UPDATE` or `DELETE` statements
- Statements without `WHERE` require `allowAffectAllRows=true`
- `execute_data_change` requires a `previewToken` by default unless confirmation is explicitly disabled

## Requirements

- .NET 10 SDK
- Access to a SQL Server instance
- An MCP client such as Codex CLI, ChatGPT Apps tooling, or another MCP-compatible host

## Configuration

`appsettings.json`

```json
{
  "SqlServerMcp": {
    "HttpPath": "/mcp",
    "SkipDmlConfirmation": false,
    "PreviewSampleLimit": 10
  }
}
```

Equivalent environment variables:

- `SqlServerMcp__HttpPath`
- `SqlServerMcp__SkipDmlConfirmation`
- `SqlServerMcp__PreviewSampleLimit`

## Run Locally

HTTP transport:

```bash
dotnet run --project ./SqlServerMcp/SqlServerMcp.csproj
```

`stdio` transport:

```bash
dotnet run --project ./SqlServerMcp/SqlServerMcp.csproj -- --transport stdio
```

## MCP Client Examples

HTTP server:

```json
{
  "servers": {
    "SqlServerMcp": {
      "type": "http",
      "url": "http://localhost:6191/mcp"
    }
  }
}
```

`stdio` server:

```json
{
  "servers": {
    "SqlServerMcp": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "/absolute/path/to/SqlServerMcp/SqlServerMcp.csproj",
        "--",
        "--transport",
        "stdio"
      ]
    }
  }
}
```

## Codex CLI Example

```toml
[mcp_servers.SqlServerMcp]
command = "dotnet"
args = [
  "run",
  "--project",
  "/absolute/path/to/SqlServerMcp/SqlServerMcp.csproj",
  "--",
  "--transport",
  "stdio"
]
startup_timeout_sec = 30.0
```

If you need environment variables for the `stdio` process, set them before launching Codex or register the server with `codex mcp add --env`:

```powershell
codex mcp add SqlServerMcp --env SqlServerMcp__SkipDmlConfirmation=false --env SqlServerMcp__PreviewSampleLimit=10 -- dotnet run --project D:/absolute/path/to/SqlServerMcp/SqlServerMcp.csproj -- --transport stdio
```

For an already running HTTP instance:

```toml
[mcp_servers.SqlServerMcp]
url = "http://localhost:6191/mcp"
startup_timeout_sec = 30.0
```

## Security Notes

- The HTTP transport does not implement authentication by itself
- Treat this service as internal unless protected by network controls, a reverse proxy, or external authentication
- Effective permissions are determined by the `connectionString` supplied by the client
- Connection strings are used per request and are not persisted by the server

## License

This project is licensed under the MIT License. See [LICENSE](./LICENSE).
