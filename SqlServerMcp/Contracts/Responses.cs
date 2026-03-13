namespace SqlServerMcp.Contracts;

/// <summary>
/// Standard JSON envelope returned by every MCP tool in this server.
/// </summary>
public sealed record JsonToolResponse
{
    public required bool Ok { get; init; }

    public required string Operation { get; init; }

    public object? Data { get; init; }

    public TargetInfo? Target { get; init; }

    public PagingInfo? Paging { get; init; }

    public required ExecutionMetrics Metrics { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public string? Error { get; init; }

    public string? ErrorType { get; init; }

    public string? ErrorDetail { get; init; }
}

/// <summary>
/// Identifies the SQL Server instance and database targeted by an operation.
/// </summary>
public sealed record TargetInfo(string? Server, string? Database);

/// <summary>
/// Describes paging information for list and query operations.
/// </summary>
public sealed record PagingInfo(int Page, string PageSize, int ReturnedRows, bool IsUnbounded);

/// <summary>
/// Reports execution timing for a tool invocation.
/// </summary>
public sealed record ExecutionMetrics(long DurationMs);

/// <summary>
/// Contains general information about the current SQL Server instance and database.
/// </summary>
public sealed record DatabaseInfo(
    string ServerName,
    string DatabaseName,
    string ProductVersion,
    string Edition,
    string CompatibilityLevel,
    string Collation,
    string CurrentLogin,
    string CurrentUser);

/// <summary>
/// Represents a database schema and its approximate object count.
/// </summary>
public sealed record SchemaInfo(string Name, int ObjectCount);

/// <summary>
/// Represents a table or view returned by the metadata listing tools.
/// </summary>
public sealed record TableInfo(string Schema, string Name, string ObjectType, long ApproximateRowCount);

/// <summary>
/// Describes a table column.
/// </summary>
public sealed record ColumnInfo(
    string Name,
    string DataType,
    int? MaxLength,
    byte? Precision,
    byte? Scale,
    bool IsNullable,
    bool IsIdentity,
    string? DefaultDefinition);

/// <summary>
/// Describes an index defined on a table.
/// </summary>
public sealed record IndexInfo(string Name, bool IsUnique, bool IsPrimaryKey, bool IsClustered, string Columns);

/// <summary>
/// Describes a foreign key relationship on a table.
/// </summary>
public sealed record ForeignKeyInfo(string Name, string Columns, string ReferenceTable, string ReferenceColumns);

/// <summary>
/// Aggregates detailed metadata for a table.
/// </summary>
public sealed record TableDetails(
    string Schema,
    string TableName,
    long ApproximateRowCount,
    IReadOnlyList<ColumnInfo> Columns,
    IReadOnlyList<IndexInfo> Indexes,
    IReadOnlyList<ForeignKeyInfo> ForeignKeys);

/// <summary>
/// Represents the SQL definition of a database object when one is available.
/// </summary>
public sealed record ObjectDefinitionInfo(string Schema, string Name, string TypeDescription, string? Definition);

/// <summary>
/// Represents a tabular SQL query result.
/// </summary>
public sealed record QueryResult(IReadOnlyList<string> Columns, IReadOnlyList<Dictionary<string, object?>> Rows);

/// <summary>
/// Represents the preview result of a supported data change operation.
/// </summary>
public sealed record MutationPreviewResult(
    long AffectedRows,
    IReadOnlyList<Dictionary<string, object?>> SampleRows,
    string PreviewToken,
    bool RequiresConfirmation,
    bool AllowAffectAllRows);

/// <summary>
/// Represents the execution result of a supported data change operation.
/// </summary>
public sealed record MutationExecutionResult(int RowsAffected, bool ConfirmationSkipped);

/// <summary>
/// Represents the execution result of an administrative SQL operation.
/// </summary>
public sealed record AdminExecutionResult(int RowsAffected, string ExecutedSql);
