using SqlServerMcp.Contracts;

namespace SqlServerMcp.Application;

/// <summary>
/// Builds the standard JSON envelope returned by every MCP tool.
/// </summary>
public static class ToolExecution
{
    /// <summary>
    /// Executes a tool action and wraps the result or exception in the standard response envelope.
    /// </summary>
    public static async Task<JsonToolResponse> ExecuteAsync(
        string operation,
        Func<CancellationToken, Task<(object? Data, TargetInfo? Target, PagingInfo? Paging)>> action,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            var result = await action(cancellationToken).ConfigureAwait(false);
            return new JsonToolResponse
            {
                Ok = true,
                Operation = operation,
                Data = result.Data,
                Target = result.Target,
                Paging = result.Paging,
                Metrics = new ExecutionMetrics((long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds)
            };
        }
        catch (Exception ex)
        {
            return new JsonToolResponse
            {
                Ok = false,
                Operation = operation,
                Error = ex.Message,
                ErrorType = ex.GetType().FullName,
                ErrorDetail = ex.ToString(),
                Metrics = new ExecutionMetrics((long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds)
            };
        }
    }
}
