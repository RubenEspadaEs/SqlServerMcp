using System.Text.RegularExpressions;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlServerMcp.Validation;

public interface ISqlScriptAnalyzer
{
    SqlScriptAnalysis AnalyzeSelect(string sql);

    SqlScriptAnalysis AnalyzeDataChange(string sql);

    SqlScriptAnalysis AnalyzeAdmin(string sql);
}

public sealed class SqlScriptAnalyzer : ISqlScriptAnalyzer
{
    private static readonly Regex UpdateRegex = new(
        @"^\s*UPDATE\s+(?:TOP\s*\(\s*\d+\s*\)\s+)?(?<target>(?:\[[^\]]+\]|[A-Za-z_][\w$#]*)(?:\.(?:\[[^\]]+\]|[A-Za-z_][\w$#]*))?)\s+SET\s+.+?(?:\s+WHERE\s+(?<where>.+?))?\s*;?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex DeleteRegex = new(
        @"^\s*DELETE\s+(?:TOP\s*\(\s*\d+\s*\)\s+)?FROM\s+(?<target>(?:\[[^\]]+\]|[A-Za-z_][\w$#]*)(?:\.(?:\[[^\]]+\]|[A-Za-z_][\w$#]*))?)\s*(?:WHERE\s+(?<where>.+?))?\s*;?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    public SqlScriptAnalysis AnalyzeSelect(string sql)
    {
        var statement = ParseSingleStatement(sql);
        if (statement is not SelectStatement)
        {
            throw new InvalidOperationException("Only a single SELECT or WITH statement is allowed.");
        }

        return new SqlScriptAnalysis(SqlScriptKind.Query, Normalize(sql));
    }

    public SqlScriptAnalysis AnalyzeDataChange(string sql)
    {
        var statement = ParseSingleStatement(sql);

        return statement switch
        {
            UpdateStatement => BuildDataChangeAnalysis(sql, UpdateRegex),
            DeleteStatement => BuildDataChangeAnalysis(sql, DeleteRegex),
            _ => throw new InvalidOperationException("Only a single simple UPDATE or DELETE statement is allowed.")
        };
    }

    public SqlScriptAnalysis AnalyzeAdmin(string sql)
    {
        var statement = ParseSingleStatement(sql);
        if (statement is SelectStatement or InsertStatement or UpdateStatement or DeleteStatement or MergeStatement)
        {
            throw new InvalidOperationException("The admin SQL tool only accepts DDL or DCL statements.");
        }

        return new SqlScriptAnalysis(SqlScriptKind.Admin, Normalize(sql));
    }

    private static SqlScriptAnalysis BuildDataChangeAnalysis(string sql, Regex regex)
    {
        var match = regex.Match(sql.Trim());
        if (!match.Success)
        {
            throw new InvalidOperationException("Only simple single-target UPDATE or DELETE statements are supported.");
        }

        var (schema, table) = ParseTarget(match.Groups["target"].Value);
        var whereClause = match.Groups["where"].Success ? match.Groups["where"].Value.Trim() : null;

        return new SqlScriptAnalysis(
            SqlScriptKind.DataChange,
            Normalize(sql),
            schema,
            table,
            whereClause,
            !string.IsNullOrWhiteSpace(whereClause));
    }

    private static TSqlStatement ParseSingleStatement(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new InvalidOperationException("SQL cannot be empty.");
        }

        var parser = new TSql170Parser(initialQuotedIdentifiers: true);
        using var reader = new StringReader(sql);
        var fragment = parser.Parse(reader, out var errors);

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(errors[0].Message);
        }

        if (fragment is not TSqlScript script ||
            script.Batches.Count != 1 ||
            script.Batches[0].Statements.Count != 1)
        {
            throw new InvalidOperationException("Exactly one SQL statement is required.");
        }

        return script.Batches[0].Statements[0];
    }

    private static (string Schema, string Table) ParseTarget(string rawTarget)
    {
        var parts = rawTarget
            .Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim().Trim('[', ']'))
            .ToArray();

        return parts.Length switch
        {
            1 => ("dbo", parts[0]),
            2 => (parts[0], parts[1]),
            _ => throw new InvalidOperationException("Only schema-qualified or bare table names are supported.")
        };
    }

    private static string Normalize(string sql) => sql.Trim().TrimEnd(';');
}
