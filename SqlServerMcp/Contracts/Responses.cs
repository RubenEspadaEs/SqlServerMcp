namespace SqlServerMcp.Contracts;

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

public sealed record TargetInfo(string? Server, string? Database);

public sealed record PagingInfo(int Page, string PageSize, int ReturnedRows, bool IsUnbounded);

public sealed record ExecutionMetrics(long DurationMs);

public sealed record DatabaseInfo(
    string ServerName,
    string DatabaseName,
    string ProductVersion,
    string Edition,
    string CompatibilityLevel,
    string Collation,
    string CurrentLogin,
    string CurrentUser);

public sealed record SchemaInfo(string Name, int ObjectCount);

public sealed record TableInfo(string Schema, string Name, string ObjectType, long ApproximateRowCount);

public sealed record ColumnInfo(
    string Name,
    string DataType,
    int? MaxLength,
    byte? Precision,
    byte? Scale,
    bool IsNullable,
    bool IsIdentity,
    string? DefaultDefinition);

public sealed record IndexInfo(string Name, bool IsUnique, bool IsPrimaryKey, bool IsClustered, string Columns);

public sealed record ForeignKeyInfo(string Name, string Columns, string ReferenceTable, string ReferenceColumns);

public sealed record TableDetails(
    string Schema,
    string TableName,
    long ApproximateRowCount,
    IReadOnlyList<ColumnInfo> Columns,
    IReadOnlyList<IndexInfo> Indexes,
    IReadOnlyList<ForeignKeyInfo> ForeignKeys);

public sealed record ObjectDefinitionInfo(string Schema, string Name, string TypeDescription, string? Definition);

public sealed record QueryResult(IReadOnlyList<string> Columns, IReadOnlyList<Dictionary<string, object?>> Rows);

public sealed record MutationPreviewResult(
    long AffectedRows,
    IReadOnlyList<Dictionary<string, object?>> SampleRows,
    string PreviewToken,
    bool RequiresConfirmation,
    bool AllowAffectAllRows);

public sealed record MutationExecutionResult(int RowsAffected, bool ConfirmationSkipped);

public sealed record AdminExecutionResult(int RowsAffected, string ExecutedSql);
