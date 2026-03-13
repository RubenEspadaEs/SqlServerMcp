
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using SqlServerMcp.Application;
using SqlServerMcp.Configuration;
using SqlServerMcp.Infrastructure.Sql;
using SqlServerMcp.OpenApi;
using SqlServerMcp.Serialization;
using SqlServerMcp.Tools;
using SqlServerMcp.Validation;

var transport = StartupArguments.ReadTransport(args);

return transport switch
{
    TransportMode.Stdio => await Bootstrap.RunStdioAsync(args),
    _ => await Bootstrap.RunHttpAsync(args),
};

internal static class Bootstrap
{
    /// <summary>
    /// Runs the MCP server over the stdio transport.
    /// </summary>
    /// <param name="args">The command-line arguments provided to the process.</param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> RunStdioAsync(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        ConfigureSharedServices(builder.Services, builder.Configuration);

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<MetadataTools>(JsonOptionsFactory.Create())
            .WithTools<DataTools>(JsonOptionsFactory.Create())
            .WithTools<AdminTools>(JsonOptionsFactory.Create());

        await builder.Build().RunAsync();
        return 0;
    }

    /// <summary>
    /// Runs the MCP server over the HTTP transport and publishes Swagger documentation for the transport surface.
    /// </summary>
    /// <param name="args">The command-line arguments provided to the process.</param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> RunHttpAsync(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        ConfigureSharedServices(builder.Services, builder.Configuration);

        builder.Services
            .AddEndpointsApiExplorer()
            .AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "SqlServerMcp HTTP Transport",
                    Version = "v1",
                    Description = "OpenAPI description of the HTTP surface used by the SqlServerMcp server."
                });

                var xmlFile = $"{typeof(Bootstrap).Assembly.GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                {
                    options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
                }

                options.DocumentFilter<McpTransportDocumentFilter>();
            });

        builder.Services
            .AddMcpServer()
            .WithHttpTransport()
            .WithTools<MetadataTools>(JsonOptionsFactory.Create())
            .WithTools<DataTools>(JsonOptionsFactory.Create())
            .WithTools<AdminTools>(JsonOptionsFactory.Create());

        var app = builder.Build();
        var options = app.Services.GetRequiredService<IOptions<SqlServerMcpOptions>>().Value;

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "SqlServerMcp HTTP Transport v1");
            options.RoutePrefix = "swagger";
        });

        app.MapGet("/", GetServerInfo)
            .WithName("GetServerInfo")
            .WithTags("Service Metadata")
            .WithSummary("Gets basic HTTP server metadata.")
            .WithDescription("Returns a lightweight HTTP status payload with the MCP endpoint path and selected runtime options.");

        app.MapMcp(options.HttpPath);
        await app.RunAsync();
        return 0;
    }

    /// <summary>
    /// Registers shared services used by both the stdio and HTTP transports.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    private static void ConfigureSharedServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SqlServerMcpOptions>(configuration.GetSection(SqlServerMcpOptions.SectionName));
        services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
        services.AddSingleton<ISqlScriptAnalyzer, SqlScriptAnalyzer>();
        services.AddSingleton<IPreviewTokenStore, InMemoryPreviewTokenStore>();
        services.AddSingleton<ISqlMetadataService, SqlMetadataService>();
        services.AddSingleton<ISqlQueryService, SqlQueryService>();
        services.AddSingleton<ISqlMutationService, SqlMutationService>();
        services.AddSingleton<ISqlAdminService, SqlAdminService>();
    }

    /// <summary>
    /// Returns a small HTTP payload describing the running server.
    /// </summary>
    /// <param name="options">The current server options.</param>
    /// <returns>The service metadata payload.</returns>
    private static IResult GetServerInfo(IOptions<SqlServerMcpOptions> options) =>
        Results.Ok(new
        {
            server = "SqlServerMcp",
            transport = "http",
            endpoint = options.Value.HttpPath,
            skipDmlConfirmation = options.Value.SkipDmlConfirmation
        });
}

internal enum TransportMode
{
    Http,
    Stdio
}

internal static class StartupArguments
{
    /// <summary>
    /// Reads the desired transport mode from the command-line arguments.
    /// </summary>
    /// <param name="args">The command-line arguments provided to the process.</param>
    /// <returns>The selected transport mode.</returns>
    public static TransportMode ReadTransport(string[] args)
    {
        for (var index = 0; index < args.Length; index++)
        {
            if (!args[index].Equals("--transport", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (index + 1 >= args.Length)
            {
                break;
            }

            return args[index + 1].Equals("stdio", StringComparison.OrdinalIgnoreCase)
                ? TransportMode.Stdio
                : TransportMode.Http;
        }

        return TransportMode.Http;
    }
}

internal static class JsonOptionsFactory
{
    /// <summary>
    /// Creates the JSON serializer options used to marshal MCP tool parameters and responses.
    /// </summary>
    /// <returns>The configured serializer options instance.</returns>
    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
        return options;
    }
}
