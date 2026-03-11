namespace SqlServerMcp.Configuration;

public sealed class SqlServerMcpOptions
{
    public const string SectionName = "SqlServerMcp";

    public string HttpPath { get; set; } = "/mcp";

    public bool SkipDmlConfirmation { get; set; }

    public int PreviewSampleLimit { get; set; } = 10;
}
