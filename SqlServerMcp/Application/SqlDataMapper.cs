using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace SqlServerMcp.Application;

internal static class SqlDataMapper
{
    public static async Task<IReadOnlyList<Dictionary<string, object?>>> ReadRowsAsync(
        DbDataReader reader,
        PaginationRequest pagination,
        CancellationToken cancellationToken)
    {
        var rows = new List<Dictionary<string, object?>>();
        var skipped = 0;

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!pagination.IsUnbounded && skipped < pagination.Offset)
            {
                skipped++;
                continue;
            }

            rows.Add(ReadCurrentRow(reader));

            if (!pagination.IsUnbounded && rows.Count >= pagination.Limit)
            {
                break;
            }
        }

        return rows;
    }

    public static Dictionary<string, object?> ReadCurrentRow(IDataRecord record)
    {
        var row = new Dictionary<string, object?>(record.FieldCount, StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < record.FieldCount; index++)
        {
            row[record.GetName(index)] = record.IsDBNull(index) ? null : NormalizeDbValue(record.GetValue(index));
        }

        return row;
    }

    public static IReadOnlyList<string> ReadColumns(IDataRecord record)
    {
        var columns = new List<string>(record.FieldCount);
        for (var index = 0; index < record.FieldCount; index++)
        {
            columns.Add(record.GetName(index));
        }

        return columns;
    }

    public static void AddParameters(SqlCommand command, IReadOnlyDictionary<string, JsonElement>? parameters)
    {
        if (parameters is null)
        {
            return;
        }

        foreach (var pair in parameters)
        {
            command.Parameters.AddWithValue(
                pair.Key.StartsWith('@') ? pair.Key : $"@{pair.Key}",
                ConvertJsonValue(pair.Value) ?? DBNull.Value);
        }
    }

    private static object? ConvertJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String when element.TryGetDateTime(out var dateTime) => dateTime,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt32(out var intValue) => intValue,
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number when element.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => element.GetRawText()
        };
    }

    private static object? NormalizeDbValue(object value) =>
        value switch
        {
            DateOnly dateOnly => dateOnly.ToString("O"),
            TimeOnly timeOnly => timeOnly.ToString("O"),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O"),
            DateTime dateTime => dateTime.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(dateTime, DateTimeKind.Utc).ToString("O")
                : dateTime.ToString("O"),
            byte[] bytes => Convert.ToBase64String(bytes),
            _ => value
        };
}
