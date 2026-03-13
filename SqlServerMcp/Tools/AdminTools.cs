using System.ComponentModel;
using ModelContextProtocol.Server;
using SqlServerMcp.Application;
using SqlServerMcp.Contracts;

namespace SqlServerMcp.Tools;

/// <summary>
/// Exposes MCP tools for administrative SQL Server operations.
/// </summary>
internal sealed class AdminTools
{
    /// <summary>
    /// Creates a table using a typed contract instead of raw SQL.
    /// </summary>
    [McpServerTool, Description("Creates a table using a typed JSON contract.")]
    public static Task<JsonToolResponse> CreateTable(
        ISqlAdminService service,
        CreateTableRequest request,
        CancellationToken cancellationToken) =>
        Execute("create_table", ct => service.CreateTableAsync(request, ct), cancellationToken);

    /// <summary>
    /// Alters a table using an ordered list of typed operations.
    /// </summary>
    [McpServerTool, Description("Alters a table using ordered typed operations.")]
    public static Task<JsonToolResponse> AlterTable(
        ISqlAdminService service,
        AlterTableRequest request,
        CancellationToken cancellationToken) =>
        Execute("alter_table", ct => service.AlterTableAsync(request, ct), cancellationToken);

    /// <summary>
    /// Creates a SQL Server login.
    /// </summary>
    [McpServerTool, Description("Creates a SQL Server login.")]
    public static Task<JsonToolResponse> CreateLogin(
        ISqlAdminService service,
        CreateLoginRequest request,
        CancellationToken cancellationToken) =>
        Execute("create_login", ct => service.CreateLoginAsync(request, ct), cancellationToken);

    /// <summary>
    /// Creates a database user and optionally assigns role memberships.
    /// </summary>
    [McpServerTool, Description("Creates a database user and optional role memberships.")]
    public static Task<JsonToolResponse> CreateUser(
        ISqlAdminService service,
        CreateUserRequest request,
        CancellationToken cancellationToken) =>
        Execute("create_user", ct => service.CreateUserAsync(request, ct), cancellationToken);

    /// <summary>
    /// Grants a database role to an existing principal.
    /// </summary>
    [McpServerTool, Description("Adds a principal to a database role.")]
    public static Task<JsonToolResponse> GrantRoleMembership(
        ISqlAdminService service,
        GrantRoleMembershipRequest request,
        CancellationToken cancellationToken) =>
        Execute("grant_role_membership", ct => service.GrantRoleMembershipAsync(request, ct), cancellationToken);

    /// <summary>
    /// Executes a single administrative DDL or DCL statement.
    /// </summary>
    [McpServerTool, Description("Executes a single DDL or DCL statement for advanced administration.")]
    public static Task<JsonToolResponse> ExecuteAdminSql(
        ISqlAdminService service,
        ExecuteAdminSqlRequest request,
        CancellationToken cancellationToken) =>
        Execute("execute_admin_sql", ct => service.ExecuteAdminSqlAsync(request, ct), cancellationToken);

    /// <summary>
    /// Executes an administrative operation and wraps the result in the standard JSON tool envelope.
    /// </summary>
    private static Task<JsonToolResponse> Execute(
        string operation,
        Func<CancellationToken, Task<(AdminExecutionResult Result, TargetInfo Target)>> action,
        CancellationToken cancellationToken) =>
        ToolExecution.ExecuteAsync(
            operation,
            async ct =>
            {
                var (result, target) = await action(ct).ConfigureAwait(false);
                return (result, target, null);
            },
            cancellationToken);
}
