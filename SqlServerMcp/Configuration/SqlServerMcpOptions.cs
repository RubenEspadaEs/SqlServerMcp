namespace SqlServerMcp.Configuration;

/// <summary>
/// Represents the runtime options used by the SqlServerMcp server.
/// </summary>
public sealed class SqlServerMcpOptions
{
    /// <summary>
    /// Gets the configuration section name for the server options.
    /// </summary>
    public const string SectionName = "SqlServerMcp";

    /// <summary>
    /// Gets or sets the route prefix where the MCP HTTP transport is published.
    /// </summary>
    public string HttpPath { get; set; } = "/mcp";

    /// <summary>
    /// Gets or sets a value indicating whether DML execution can skip the preview token confirmation step.
    /// </summary>
    public bool SkipDmlConfirmation { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of preview rows returned by mutation preview operations.
    /// </summary>
    public int PreviewSampleLimit { get; set; } = 10;
}
