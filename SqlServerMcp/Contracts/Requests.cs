using System.ComponentModel;
using System.Text.Json;

namespace SqlServerMcp.Contracts;

public abstract record SqlConnectionRequest
{
    [Description("SQL Server ADO.NET connection string. It must be sent on every call.")]
    public string ConnectionString { get; init; } = string.Empty;

    [Description("Optional command timeout in seconds.")]
    public int? CommandTimeoutSeconds { get; init; }

    [Description("Page number for paged responses. Starts at 1.")]
    public int Page { get; init; } = 1;

    [Description("Page size as a string. Use 25 by default, 0 or * to return all rows.")]
    public string? PageSize { get; init; } = "25";
}

public sealed record DatabaseInfoRequest : SqlConnectionRequest;

public sealed record ListSchemasRequest : SqlConnectionRequest
{
    [Description("Include system schemas like sys and INFORMATION_SCHEMA.")]
    public bool IncludeSystemSchemas { get; init; }
}

public sealed record ListTablesRequest : SqlConnectionRequest
{
    [Description("Optional schema filter.")]
    public string? Schema { get; init; }

    [Description("Include views in addition to base tables.")]
    public bool IncludeViews { get; init; }

    [Description("Include system-shipped objects.")]
    public bool IncludeSystemObjects { get; init; }
}

public sealed record TableDetailsRequest : SqlConnectionRequest
{
    [Description("Schema name.")]
    public string Schema { get; init; } = "dbo";

    [Description("Table name.")]
    public string TableName { get; init; } = string.Empty;
}

public sealed record ObjectDefinitionRequest : SqlConnectionRequest
{
    [Description("Object schema. Optional when object name is already schema-qualified.")]
    public string? Schema { get; init; }

    [Description("Object name or schema-qualified object name.")]
    public string ObjectName { get; init; } = string.Empty;
}

public sealed record QuerySqlRequest : SqlConnectionRequest
{
    [Description("Single SELECT or WITH statement.")]
    public string Sql { get; init; } = string.Empty;

    [Description("Optional named SQL parameters.")]
    public Dictionary<string, JsonElement>? Parameters { get; init; }
}

public sealed record PreviewDataChangeRequest : SqlConnectionRequest
{
    [Description("Single UPDATE or DELETE statement. Only simple single-target DML is supported.")]
    public string Sql { get; init; } = string.Empty;

    [Description("Optional named SQL parameters.")]
    public Dictionary<string, JsonElement>? Parameters { get; init; }

    [Description("Explicitly allow touching all rows when the statement does not have a WHERE clause.")]
    public bool AllowAffectAllRows { get; init; }
}

public sealed record ExecuteDataChangeRequest : SqlConnectionRequest
{
    [Description("Preview token returned by preview_data_change.")]
    public string PreviewToken { get; init; } = string.Empty;

    [Description("The same UPDATE or DELETE statement used during preview.")]
    public string Sql { get; init; } = string.Empty;

    [Description("Optional named SQL parameters.")]
    public Dictionary<string, JsonElement>? Parameters { get; init; }

    [Description("Explicitly allow touching all rows when the statement does not have a WHERE clause.")]
    public bool AllowAffectAllRows { get; init; }
}

public sealed record CreateTableRequest : SqlConnectionRequest
{
    public string Schema { get; init; } = "dbo";

    public string TableName { get; init; } = string.Empty;

    public IReadOnlyList<ColumnDefinition> Columns { get; init; } = [];

    public PrimaryKeyDefinition? PrimaryKey { get; init; }

    public IReadOnlyList<IndexDefinition> Indexes { get; init; } = [];

    public IReadOnlyList<ForeignKeyDefinition> ForeignKeys { get; init; } = [];
}

public sealed record AlterTableRequest : SqlConnectionRequest
{
    public string Schema { get; init; } = "dbo";

    public string TableName { get; init; } = string.Empty;

    public IReadOnlyList<AlterTableOperation> Operations { get; init; } = [];
}

public sealed record CreateLoginRequest : SqlConnectionRequest
{
    [Description("Login type: sql or windows.")]
    public string LoginType { get; init; } = "sql";

    public string LoginName { get; init; } = string.Empty;

    public string? Password { get; init; }

    public bool CheckPolicy { get; init; } = true;

    public bool CheckExpiration { get; init; }

    public string? DefaultDatabase { get; init; }
}

public sealed record CreateUserRequest : SqlConnectionRequest
{
    public string UserName { get; init; } = string.Empty;

    public string? LoginName { get; init; }

    public string? DefaultSchema { get; init; }

    public IReadOnlyList<string> Roles { get; init; } = [];
}

public sealed record GrantRoleMembershipRequest : SqlConnectionRequest
{
    public string RoleName { get; init; } = string.Empty;

    public string PrincipalName { get; init; } = string.Empty;
}

public sealed record ExecuteAdminSqlRequest : SqlConnectionRequest
{
    [Description("Single DDL or DCL statement.")]
    public string Sql { get; init; } = string.Empty;

    [Description("Optional named SQL parameters.")]
    public Dictionary<string, JsonElement>? Parameters { get; init; }
}

public sealed record ColumnDefinition
{
    public string Name { get; init; } = string.Empty;

    public string DataType { get; init; } = string.Empty;

    public bool IsNullable { get; init; }

    public bool IsIdentity { get; init; }

    public int IdentitySeed { get; init; } = 1;

    public int IdentityIncrement { get; init; } = 1;

    public string? DefaultSql { get; init; }
}

public sealed record PrimaryKeyDefinition
{
    public string Name { get; init; } = string.Empty;

    public IReadOnlyList<string> Columns { get; init; } = [];

    public bool Clustered { get; init; } = true;
}

public sealed record IndexDefinition
{
    public string Name { get; init; } = string.Empty;

    public IReadOnlyList<string> Columns { get; init; } = [];

    public bool IsUnique { get; init; }

    public bool IsClustered { get; init; }
}

public sealed record ForeignKeyDefinition
{
    public string Name { get; init; } = string.Empty;

    public IReadOnlyList<string> Columns { get; init; } = [];

    public string ReferenceSchema { get; init; } = "dbo";

    public string ReferenceTable { get; init; } = string.Empty;

    public IReadOnlyList<string> ReferenceColumns { get; init; } = [];

    public string? OnDeleteAction { get; init; }

    public string? OnUpdateAction { get; init; }
}

public sealed record AlterTableOperation
{
    [Description("Supported values: add_column, alter_column, drop_column, add_constraint, drop_constraint, rename_column, rename_table.")]
    public string Operation { get; init; } = string.Empty;

    public string? Name { get; init; }

    public string? NewName { get; init; }

    public string? DataType { get; init; }

    public bool? IsNullable { get; init; }

    public bool IsIdentity { get; init; }

    public int IdentitySeed { get; init; } = 1;

    public int IdentityIncrement { get; init; } = 1;

    public string? DefaultSql { get; init; }

    [Description("Used by add_constraint. Example: CONSTRAINT [CK_Table] CHECK ([Value] > 0)")]
    public string? DefinitionSql { get; init; }
}
