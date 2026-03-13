namespace SqlServerMcp.Validation;

/// <summary>
/// Identifies the supported SQL statement categories handled by the server.
/// </summary>
public enum SqlScriptKind
{
    Query,
    DataChange,
    Admin
}

/// <summary>
/// Represents the normalized analysis of a SQL statement.
/// </summary>
public sealed record SqlScriptAnalysis(
    SqlScriptKind Kind,
    string NormalizedSql,
    string? TargetSchema = null,
    string? TargetTable = null,
    string? WhereClause = null,
    bool HasWhereClause = false);
