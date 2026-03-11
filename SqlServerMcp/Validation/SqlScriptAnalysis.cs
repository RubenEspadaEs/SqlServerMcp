namespace SqlServerMcp.Validation;

public enum SqlScriptKind
{
    Query,
    DataChange,
    Admin
}

public sealed record SqlScriptAnalysis(
    SqlScriptKind Kind,
    string NormalizedSql,
    string? TargetSchema = null,
    string? TargetTable = null,
    string? WhereClause = null,
    bool HasWhereClause = false);
