using System.ComponentModel;
using ModelContextProtocol.Server;
using SqlServerMcp.Application;
using SqlServerMcp.Contracts;

namespace SqlServerMcp.Tools;

internal sealed class AdminTools
{
    [McpServerTool, Description("Creates a table using a typed JSON contract.")]
    public static Task<JsonToolResponse> CreateTable(
        ISqlAdminService service,
        CreateTableRequest request,
        CancellationToken cancellationToken) =>
        Execute("create_table", ct => service.CreateTableAsync(request, ct), cancellationToken);

    [McpServerTool, Description("Alters a table using ordered typed operations.")]
    public static Task<JsonToolResponse> AlterTable(
        ISqlAdminService service,
        AlterTableRequest request,
        CancellationToken cancellationToken) =>
        Execute("alter_table", ct => service.AlterTableAsync(request, ct), cancellationToken);

    [McpServerTool, Description("Creates a SQL Server login.")]
    public static Task<JsonToolResponse> CreateLogin(
        ISqlAdminService service,
        CreateLoginRequest request,
        CancellationToken cancellationToken) =>
        Execute("create_login", ct => service.CreateLoginAsync(request, ct), cancellationToken);

    [McpServerTool, Description("Creates a database user and optional role memberships.")]
    public static Task<JsonToolResponse> CreateUser(
        ISqlAdminService service,
        CreateUserRequest request,
        CancellationToken cancellationToken) =>
        Execute("create_user", ct => service.CreateUserAsync(request, ct), cancellationToken);

    [McpServerTool, Description("Adds a principal to a database role.")]
    public static Task<JsonToolResponse> GrantRoleMembership(
        ISqlAdminService service,
        GrantRoleMembershipRequest request,
        CancellationToken cancellationToken) =>
        Execute("grant_role_membership", ct => service.GrantRoleMembershipAsync(request, ct), cancellationToken);

    [McpServerTool, Description("Executes a single DDL or DCL statement for advanced administration.")]
    public static Task<JsonToolResponse> ExecuteAdminSql(
        ISqlAdminService service,
        ExecuteAdminSqlRequest request,
        CancellationToken cancellationToken) =>
        Execute("execute_admin_sql", ct => service.ExecuteAdminSqlAsync(request, ct), cancellationToken);

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
