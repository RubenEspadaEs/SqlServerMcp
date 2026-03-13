namespace SqlServerMcp.Application;

/// <summary>
/// Parses page arguments into a normalized paging request.
/// </summary>
public static class PaginationParser
{
    /// <summary>
    /// Converts the raw page inputs into a normalized paging request.
    /// </summary>
    public static PaginationRequest Parse(int page, string? pageSize)
    {
        var normalizedPage = page <= 0 ? 1 : page;
        var normalizedPageSize = string.IsNullOrWhiteSpace(pageSize) ? "25" : pageSize.Trim();

        if (normalizedPageSize is "*" or "0")
        {
            return new PaginationRequest(normalizedPage, normalizedPageSize, 0, 0, true);
        }

        if (!int.TryParse(normalizedPageSize, out var parsedPageSize) || parsedPageSize <= 0)
        {
            throw new InvalidOperationException("PageSize must be a positive number, 0, or *.");
        }

        return new PaginationRequest(normalizedPage, normalizedPageSize, (normalizedPage - 1) * parsedPageSize, parsedPageSize, false);
    }
}

/// <summary>
/// Represents a normalized paging request ready to be applied during data reads.
/// </summary>
public sealed record PaginationRequest(int Page, string PageSizeLabel, int Offset, int Limit, bool IsUnbounded);
