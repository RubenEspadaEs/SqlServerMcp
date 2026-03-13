using System.Data;
using Microsoft.Data.SqlClient;
using SqlServerMcp.Contracts;
using SqlServerMcp.Infrastructure.Sql;

namespace SqlServerMcp.Application;

/// <summary>
/// Defines metadata-oriented SQL Server operations exposed by the MCP tools.
/// </summary>
public interface ISqlMetadataService
{
    /// <summary>
    /// Returns general information about the current SQL Server instance and database.
    /// </summary>
    Task<(DatabaseInfo Info, TargetInfo Target)> GetDatabaseInfoAsync(DatabaseInfoRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Lists the schemas available in the current database.
    /// </summary>
    Task<(IReadOnlyList<SchemaInfo> Schemas, TargetInfo Target, PagingInfo Paging)> ListSchemasAsync(ListSchemasRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Lists tables and optionally views in the current database.
    /// </summary>
    Task<(IReadOnlyList<TableInfo> Tables, TargetInfo Target, PagingInfo Paging)> ListTablesAsync(ListTablesRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Returns detailed metadata for a single table.
    /// </summary>
    Task<(TableDetails Details, TargetInfo Target)> GetTableDetailsAsync(TableDetailsRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the SQL definition of a database object when one is available.
    /// </summary>
    Task<(ObjectDefinitionInfo Definition, TargetInfo Target)> GetObjectDefinitionAsync(ObjectDefinitionRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Implements metadata-oriented SQL Server operations for the MCP tools.
/// </summary>
public sealed class SqlMetadataService(ISqlConnectionFactory connectionFactory) : ISqlMetadataService
{
    /// <inheritdoc />
    public async Task<(DatabaseInfo Info, TargetInfo Target)> GetDatabaseInfoAsync(DatabaseInfoRequest request, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(request.ConnectionString, request.CommandTimeoutSeconds, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                @@SERVERNAME AS ServerName,
                DB_NAME() AS DatabaseName,
                CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128)) AS ProductVersion,
                CAST(SERVERPROPERTY('Edition') AS nvarchar(128)) AS Edition,
                CAST(d.compatibility_level AS nvarchar(10)) AS CompatibilityLevel,
                CAST(DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS nvarchar(128)) AS Collation,
                ORIGINAL_LOGIN() AS CurrentLogin,
                CURRENT_USER AS CurrentUser
            FROM sys.databases d
            WHERE d.name = DB_NAME();
            """;

        using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken).ConfigureAwait(false);
        await reader.ReadAsync(cancellationToken).ConfigureAwait(false);

        var info = new DatabaseInfo(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetString(7));

        return (info, new TargetInfo(connection.DataSource, connection.Database));
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<SchemaInfo> Schemas, TargetInfo Target, PagingInfo Paging)> ListSchemasAsync(ListSchemasRequest request, CancellationToken cancellationToken)
    {
        var pagination = PaginationParser.Parse(request.Page, request.PageSize);
        await using var connection = await connectionFactory.OpenAsync(request.ConnectionString, request.CommandTimeoutSeconds, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                s.name AS SchemaName,
                COUNT(o.object_id) AS ObjectCount
            FROM sys.schemas s
            LEFT JOIN sys.objects o ON o.schema_id = s.schema_id AND o.is_ms_shipped = 0
            WHERE (@includeSystemSchemas = 1 OR s.name NOT IN ('sys', 'INFORMATION_SCHEMA'))
            GROUP BY s.name
            ORDER BY s.name;
            """;
        command.Parameters.AddWithValue("@includeSystemSchemas", request.IncludeSystemSchemas);

        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var rows = await SqlDataMapper.ReadRowsAsync(reader, pagination, cancellationToken).ConfigureAwait(false);
        var data = rows.Select(row => new SchemaInfo((string)row["SchemaName"]!, Convert.ToInt32(row["ObjectCount"]))).ToArray();

        return (data, new TargetInfo(connection.DataSource, connection.Database), new PagingInfo(pagination.Page, pagination.PageSizeLabel, data.Length, pagination.IsUnbounded));
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<TableInfo> Tables, TargetInfo Target, PagingInfo Paging)> ListTablesAsync(ListTablesRequest request, CancellationToken cancellationToken)
    {
        var pagination = PaginationParser.Parse(request.Page, request.PageSize);
        await using var connection = await connectionFactory.OpenAsync(request.ConnectionString, request.CommandTimeoutSeconds, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                s.name AS SchemaName,
                o.name AS ObjectName,
                o.type_desc AS ObjectType,
                ISNULL(SUM(CASE WHEN p.index_id IN (0,1) THEN p.row_count END), 0) AS ApproximateRowCount
            FROM sys.objects o
            INNER JOIN sys.schemas s ON s.schema_id = o.schema_id
            LEFT JOIN sys.dm_db_partition_stats p ON p.object_id = o.object_id
            WHERE
                (
                    o.type = 'U'
                    OR (@includeViews = 1 AND o.type = 'V')
                )
                AND (@schema IS NULL OR s.name = @schema)
                AND (@includeSystemObjects = 1 OR o.is_ms_shipped = 0)
            GROUP BY s.name, o.name, o.type_desc
            ORDER BY s.name, o.name;
            """;
        command.Parameters.AddWithValue("@includeViews", request.IncludeViews);
        command.Parameters.AddWithValue("@schema", (object?)request.Schema ?? DBNull.Value);
        command.Parameters.AddWithValue("@includeSystemObjects", request.IncludeSystemObjects);

        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var rows = await SqlDataMapper.ReadRowsAsync(reader, pagination, cancellationToken).ConfigureAwait(false);
        var data = rows.Select(row => new TableInfo(
            (string)row["SchemaName"]!,
            (string)row["ObjectName"]!,
            (string)row["ObjectType"]!,
            Convert.ToInt64(row["ApproximateRowCount"]))).ToArray();

        return (data, new TargetInfo(connection.DataSource, connection.Database), new PagingInfo(pagination.Page, pagination.PageSizeLabel, data.Length, pagination.IsUnbounded));
    }

    /// <inheritdoc />
    public async Task<(TableDetails Details, TargetInfo Target)> GetTableDetailsAsync(TableDetailsRequest request, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(request.ConnectionString, request.CommandTimeoutSeconds, cancellationToken).ConfigureAwait(false);

        var rowCount = await GetApproximateRowCountAsync(connection, request.Schema, request.TableName, cancellationToken).ConfigureAwait(false);
        var columns = await GetColumnsAsync(connection, request.Schema, request.TableName, cancellationToken).ConfigureAwait(false);
        var indexes = await GetIndexesAsync(connection, request.Schema, request.TableName, cancellationToken).ConfigureAwait(false);
        var foreignKeys = await GetForeignKeysAsync(connection, request.Schema, request.TableName, cancellationToken).ConfigureAwait(false);

        return (new TableDetails(request.Schema, request.TableName, rowCount, columns, indexes, foreignKeys), new TargetInfo(connection.DataSource, connection.Database));
    }

    /// <inheritdoc />
    public async Task<(ObjectDefinitionInfo Definition, TargetInfo Target)> GetObjectDefinitionAsync(ObjectDefinitionRequest request, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(request.ConnectionString, request.CommandTimeoutSeconds, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TOP (1)
                s.name AS SchemaName,
                o.name AS ObjectName,
                o.type_desc AS TypeDescription,
                m.definition AS ObjectDefinition
            FROM sys.objects o
            INNER JOIN sys.schemas s ON s.schema_id = o.schema_id
            LEFT JOIN sys.sql_modules m ON m.object_id = o.object_id
            WHERE
                (@schema IS NULL OR s.name = @schema)
                AND (s.name + '.' + o.name = @qualifiedName OR o.name = @objectName)
            ORDER BY CASE WHEN s.name + '.' + o.name = @qualifiedName THEN 0 ELSE 1 END;
            """;
        command.Parameters.AddWithValue("@schema", (object?)request.Schema ?? DBNull.Value);
        command.Parameters.AddWithValue("@qualifiedName", request.ObjectName);
        command.Parameters.AddWithValue("@objectName", request.ObjectName.Contains('.', StringComparison.Ordinal) ? request.ObjectName.Split('.')[^1] : request.ObjectName);

        using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The requested object was not found.");
        }

        var info = new ObjectDefinitionInfo(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3));

        return (info, new TargetInfo(connection.DataSource, connection.Database));
    }

    private static async Task<long> GetApproximateRowCountAsync(SqlConnection connection, string schema, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ISNULL(SUM(CASE WHEN p.index_id IN (0,1) THEN p.row_count END), 0)
            FROM sys.tables t
            INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
            LEFT JOIN sys.dm_db_partition_stats p ON p.object_id = t.object_id
            WHERE s.name = @schema AND t.name = @tableName;
            """;
        command.Parameters.AddWithValue("@schema", schema);
        command.Parameters.AddWithValue("@tableName", tableName);

        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
    }

    private static async Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(SqlConnection connection, string schema, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                c.name,
                ty.name,
                c.max_length,
                c.precision,
                c.scale,
                c.is_nullable,
                c.is_identity,
                dc.definition
            FROM sys.columns c
            INNER JOIN sys.tables t ON t.object_id = c.object_id
            INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
            INNER JOIN sys.types ty ON ty.user_type_id = c.user_type_id
            LEFT JOIN sys.default_constraints dc ON dc.object_id = c.default_object_id
            WHERE s.name = @schema AND t.name = @tableName
            ORDER BY c.column_id;
            """;
        command.Parameters.AddWithValue("@schema", schema);
        command.Parameters.AddWithValue("@tableName", tableName);

        var items = new List<ColumnInfo>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            items.Add(new ColumnInfo(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetInt16(2),
                reader.IsDBNull(3) ? null : reader.GetByte(3),
                reader.IsDBNull(4) ? null : reader.GetByte(4),
                reader.GetBoolean(5),
                reader.GetBoolean(6),
                reader.IsDBNull(7) ? null : reader.GetString(7)));
        }

        return items;
    }

    private static async Task<IReadOnlyList<IndexInfo>> GetIndexesAsync(SqlConnection connection, string schema, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                i.name,
                i.is_unique,
                i.is_primary_key,
                i.type_desc,
                STRING_AGG(c.name, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS Columns
            FROM sys.indexes i
            INNER JOIN sys.tables t ON t.object_id = i.object_id
            INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
            INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE s.name = @schema AND t.name = @tableName AND i.name IS NOT NULL
            GROUP BY i.name, i.is_unique, i.is_primary_key, i.type_desc
            ORDER BY i.is_primary_key DESC, i.name;
            """;
        command.Parameters.AddWithValue("@schema", schema);
        command.Parameters.AddWithValue("@tableName", tableName);

        var items = new List<IndexInfo>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            items.Add(new IndexInfo(
                reader.GetString(0),
                reader.GetBoolean(1),
                reader.GetBoolean(2),
                reader.GetString(3).Contains("CLUSTERED", StringComparison.OrdinalIgnoreCase),
                reader.GetString(4)));
        }

        return items;
    }

    private static async Task<IReadOnlyList<ForeignKeyInfo>> GetForeignKeysAsync(SqlConnection connection, string schema, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                fk.name,
                STRING_AGG(pc.name, ', ') WITHIN GROUP (ORDER BY fkc.constraint_column_id) AS ParentColumns,
                rs.name + '.' + rt.name AS ReferenceTable,
                STRING_AGG(rc.name, ', ') WITHIN GROUP (ORDER BY fkc.constraint_column_id) AS ReferenceColumns
            FROM sys.foreign_keys fk
            INNER JOIN sys.tables t ON t.object_id = fk.parent_object_id
            INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
            INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
            INNER JOIN sys.columns pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
            INNER JOIN sys.tables rt ON rt.object_id = fkc.referenced_object_id
            INNER JOIN sys.schemas rs ON rs.schema_id = rt.schema_id
            INNER JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
            WHERE s.name = @schema AND t.name = @tableName
            GROUP BY fk.name, rs.name, rt.name
            ORDER BY fk.name;
            """;
        command.Parameters.AddWithValue("@schema", schema);
        command.Parameters.AddWithValue("@tableName", tableName);

        var items = new List<ForeignKeyInfo>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            items.Add(new ForeignKeyInfo(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3)));
        }

        return items;
    }
}
