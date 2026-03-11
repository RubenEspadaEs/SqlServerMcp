using System.Text;
using Microsoft.Data.SqlClient;
using SqlServerMcp.Contracts;
using SqlServerMcp.Infrastructure.Sql;
using SqlServerMcp.Validation;

namespace SqlServerMcp.Application;

public interface ISqlAdminService
{
    Task<(AdminExecutionResult Result, TargetInfo Target)> CreateTableAsync(CreateTableRequest request, CancellationToken cancellationToken);

    Task<(AdminExecutionResult Result, TargetInfo Target)> AlterTableAsync(AlterTableRequest request, CancellationToken cancellationToken);

    Task<(AdminExecutionResult Result, TargetInfo Target)> CreateLoginAsync(CreateLoginRequest request, CancellationToken cancellationToken);

    Task<(AdminExecutionResult Result, TargetInfo Target)> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken);

    Task<(AdminExecutionResult Result, TargetInfo Target)> GrantRoleMembershipAsync(GrantRoleMembershipRequest request, CancellationToken cancellationToken);

    Task<(AdminExecutionResult Result, TargetInfo Target)> ExecuteAdminSqlAsync(ExecuteAdminSqlRequest request, CancellationToken cancellationToken);
}

public sealed class SqlAdminService(ISqlConnectionFactory connectionFactory, ISqlScriptAnalyzer scriptAnalyzer) : ISqlAdminService
{
    public Task<(AdminExecutionResult Result, TargetInfo Target)> CreateTableAsync(CreateTableRequest request, CancellationToken cancellationToken) =>
        ExecuteAdminCommandAsync(request.ConnectionString, request.CommandTimeoutSeconds, BuildCreateTableSql(request), null, cancellationToken);

    public Task<(AdminExecutionResult Result, TargetInfo Target)> AlterTableAsync(AlterTableRequest request, CancellationToken cancellationToken) =>
        ExecuteAdminCommandAsync(request.ConnectionString, request.CommandTimeoutSeconds, BuildAlterTableSql(request), null, cancellationToken);

    public Task<(AdminExecutionResult Result, TargetInfo Target)> CreateLoginAsync(CreateLoginRequest request, CancellationToken cancellationToken) =>
        ExecuteAdminCommandAsync(request.ConnectionString, request.CommandTimeoutSeconds, BuildCreateLoginSql(request), null, cancellationToken);

    public Task<(AdminExecutionResult Result, TargetInfo Target)> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken) =>
        ExecuteAdminCommandAsync(request.ConnectionString, request.CommandTimeoutSeconds, BuildCreateUserSql(request), null, cancellationToken);

    public Task<(AdminExecutionResult Result, TargetInfo Target)> GrantRoleMembershipAsync(GrantRoleMembershipRequest request, CancellationToken cancellationToken) =>
        ExecuteAdminCommandAsync(request.ConnectionString, request.CommandTimeoutSeconds, BuildGrantRoleSql(request), null, cancellationToken);

    public Task<(AdminExecutionResult Result, TargetInfo Target)> ExecuteAdminSqlAsync(ExecuteAdminSqlRequest request, CancellationToken cancellationToken)
    {
        scriptAnalyzer.AnalyzeAdmin(request.Sql);
        return ExecuteAdminCommandAsync(request.ConnectionString, request.CommandTimeoutSeconds, request.Sql, request.Parameters, cancellationToken);
    }

    private async Task<(AdminExecutionResult Result, TargetInfo Target)> ExecuteAdminCommandAsync(
        string connectionString,
        int? commandTimeoutSeconds,
        string sql,
        IReadOnlyDictionary<string, System.Text.Json.JsonElement>? parameters,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(connectionString, commandTimeoutSeconds, cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        if (commandTimeoutSeconds is int timeout && timeout > 0)
        {
            command.CommandTimeout = timeout;
        }

        SqlDataMapper.AddParameters(command, parameters);
        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        return (new AdminExecutionResult(rowsAffected, sql), new TargetInfo(connection.DataSource, connection.Database));
    }

    private static string BuildCreateTableSql(CreateTableRequest request)
    {
        if (request.Columns.Count == 0)
        {
            throw new InvalidOperationException("At least one column is required.");
        }

        var columnSql = request.Columns.Select(BuildColumnSql).ToList();

        if (request.PrimaryKey is not null)
        {
            var columns = string.Join(", ", request.PrimaryKey.Columns.Select(SqlIdentifier.Quote));
            var clustered = request.PrimaryKey.Clustered ? "CLUSTERED" : "NONCLUSTERED";
            columnSql.Add($"CONSTRAINT {SqlIdentifier.Quote(request.PrimaryKey.Name)} PRIMARY KEY {clustered} ({columns})");
        }

        var builder = new StringBuilder();
        builder.AppendLine($"CREATE TABLE {SqlIdentifier.Qualify(request.Schema, request.TableName)} (");
        builder.AppendLine($"    {string.Join("," + Environment.NewLine + "    ", columnSql)}");
        builder.AppendLine(");");

        foreach (var index in request.Indexes)
        {
            var unique = index.IsUnique ? "UNIQUE " : string.Empty;
            var clustered = index.IsClustered ? "CLUSTERED" : "NONCLUSTERED";
            builder.AppendLine($"CREATE {unique}{clustered} INDEX {SqlIdentifier.Quote(index.Name)} ON {SqlIdentifier.Qualify(request.Schema, request.TableName)} ({string.Join(", ", index.Columns.Select(SqlIdentifier.Quote))});");
        }

        foreach (var foreignKey in request.ForeignKeys)
        {
            builder.Append($"ALTER TABLE {SqlIdentifier.Qualify(request.Schema, request.TableName)} ADD CONSTRAINT {SqlIdentifier.Quote(foreignKey.Name)} FOREIGN KEY ({string.Join(", ", foreignKey.Columns.Select(SqlIdentifier.Quote))}) ");
            builder.Append($"REFERENCES {SqlIdentifier.Qualify(foreignKey.ReferenceSchema, foreignKey.ReferenceTable)} ({string.Join(", ", foreignKey.ReferenceColumns.Select(SqlIdentifier.Quote))})");

            if (!string.IsNullOrWhiteSpace(foreignKey.OnDeleteAction))
            {
                builder.Append($" ON DELETE {foreignKey.OnDeleteAction}");
            }

            if (!string.IsNullOrWhiteSpace(foreignKey.OnUpdateAction))
            {
                builder.Append($" ON UPDATE {foreignKey.OnUpdateAction}");
            }

            builder.AppendLine(";");
        }

        return builder.ToString();
    }

    private static string BuildColumnSql(ColumnDefinition column)
    {
        var builder = new StringBuilder();
        builder.Append($"{SqlIdentifier.Quote(column.Name)} {column.DataType}");

        if (column.IsIdentity)
        {
            builder.Append($" IDENTITY({column.IdentitySeed},{column.IdentityIncrement})");
        }

        builder.Append(column.IsNullable ? " NULL" : " NOT NULL");

        if (!string.IsNullOrWhiteSpace(column.DefaultSql))
        {
            builder.Append($" DEFAULT {column.DefaultSql}");
        }

        return builder.ToString();
    }

    private static string BuildAlterTableSql(AlterTableRequest request)
    {
        if (request.Operations.Count == 0)
        {
            throw new InvalidOperationException("At least one alter table operation is required.");
        }

        var qualifiedTable = SqlIdentifier.Qualify(request.Schema, request.TableName);
        var statements = new List<string>();

        foreach (var operation in request.Operations)
        {
            statements.Add(operation.Operation.ToLowerInvariant() switch
            {
                "add_column" => $"ALTER TABLE {qualifiedTable} ADD {BuildAddColumnSql(operation)};",
                "alter_column" => $"ALTER TABLE {qualifiedTable} ALTER COLUMN {BuildAlterColumnSql(operation)};",
                "drop_column" => $"ALTER TABLE {qualifiedTable} DROP COLUMN {SqlIdentifier.Quote(operation.Name!)};",
                "add_constraint" => $"ALTER TABLE {qualifiedTable} ADD {operation.DefinitionSql};",
                "drop_constraint" => $"ALTER TABLE {qualifiedTable} DROP CONSTRAINT {SqlIdentifier.Quote(operation.Name!)};",
                "rename_column" => $"EXEC sp_rename '{request.Schema}.{request.TableName}.{operation.Name}', '{operation.NewName}', 'COLUMN';",
                "rename_table" => $"EXEC sp_rename '{request.Schema}.{request.TableName}', '{operation.NewName}';",
                _ => throw new InvalidOperationException($"Unsupported alter table operation '{operation.Operation}'.")
            });
        }

        return string.Join(Environment.NewLine, statements);
    }

    private static string BuildAddColumnSql(AlterTableOperation operation)
    {
        if (string.IsNullOrWhiteSpace(operation.Name) || string.IsNullOrWhiteSpace(operation.DataType))
        {
            throw new InvalidOperationException("add_column requires Name and DataType.");
        }

        var builder = new StringBuilder();
        builder.Append($"{SqlIdentifier.Quote(operation.Name)} {operation.DataType}");
        if (operation.IsIdentity)
        {
            builder.Append($" IDENTITY({operation.IdentitySeed},{operation.IdentityIncrement})");
        }

        builder.Append(operation.IsNullable.GetValueOrDefault() ? " NULL" : " NOT NULL");

        if (!string.IsNullOrWhiteSpace(operation.DefaultSql))
        {
            builder.Append($" DEFAULT {operation.DefaultSql}");
        }

        return builder.ToString();
    }

    private static string BuildAlterColumnSql(AlterTableOperation operation)
    {
        if (string.IsNullOrWhiteSpace(operation.Name) || string.IsNullOrWhiteSpace(operation.DataType) || operation.IsNullable is null)
        {
            throw new InvalidOperationException("alter_column requires Name, DataType, and IsNullable.");
        }

        return $"{SqlIdentifier.Quote(operation.Name)} {operation.DataType} {(operation.IsNullable.Value ? "NULL" : "NOT NULL")}";
    }

    private static string BuildCreateLoginSql(CreateLoginRequest request)
    {
        if (string.Equals(request.LoginType, "windows", StringComparison.OrdinalIgnoreCase))
        {
            return $"CREATE LOGIN {SqlIdentifier.Quote(request.LoginName)} FROM WINDOWS;";
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new InvalidOperationException("A password is required for SQL logins.");
        }

        var builder = new StringBuilder();
        builder.Append($"CREATE LOGIN {SqlIdentifier.Quote(request.LoginName)} WITH PASSWORD = '{request.Password.Replace("'", "''", StringComparison.Ordinal)}'");
        builder.Append($", CHECK_POLICY = {(request.CheckPolicy ? "ON" : "OFF")}");
        builder.Append($", CHECK_EXPIRATION = {(request.CheckExpiration ? "ON" : "OFF")}");

        if (!string.IsNullOrWhiteSpace(request.DefaultDatabase))
        {
            builder.Append($", DEFAULT_DATABASE = {SqlIdentifier.Quote(request.DefaultDatabase)}");
        }

        builder.Append(';');
        return builder.ToString();
    }

    private static string BuildCreateUserSql(CreateUserRequest request)
    {
        var builder = new StringBuilder();
        builder.Append($"CREATE USER {SqlIdentifier.Quote(request.UserName)}");

        if (!string.IsNullOrWhiteSpace(request.LoginName))
        {
            builder.Append($" FOR LOGIN {SqlIdentifier.Quote(request.LoginName)}");
        }

        if (!string.IsNullOrWhiteSpace(request.DefaultSchema))
        {
            builder.Append($" WITH DEFAULT_SCHEMA = {SqlIdentifier.Quote(request.DefaultSchema)}");
        }

        builder.AppendLine(";");

        foreach (var role in request.Roles)
        {
            builder.AppendLine($"ALTER ROLE {SqlIdentifier.Quote(role)} ADD MEMBER {SqlIdentifier.Quote(request.UserName)};");
        }

        return builder.ToString();
    }

    private static string BuildGrantRoleSql(GrantRoleMembershipRequest request)
    {
        return $"ALTER ROLE {SqlIdentifier.Quote(request.RoleName)} ADD MEMBER {SqlIdentifier.Quote(request.PrincipalName)};";
    }
}
