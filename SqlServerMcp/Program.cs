
using System.Text.Json;
using Microsoft.Extensions.Options;
using SqlServerMcp.Application;
using SqlServerMcp.Configuration;
using SqlServerMcp.Infrastructure.Sql;
using SqlServerMcp.Serialization;
using SqlServerMcp.Tools;
using SqlServerMcp.Validation;

var transport = StartupArguments.ReadTransport(args);

return transport switch
{
    TransportMode.Stdio => await RunStdioAsync(args),
    _ => await RunHttpAsync(args),
};

static async Task<int> RunStdioAsync(string[] args)
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

static async Task<int> RunHttpAsync(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);
    ConfigureSharedServices(builder.Services, builder.Configuration);

    builder.Services
        .AddMcpServer()
        .WithHttpTransport()
        .WithTools<MetadataTools>(JsonOptionsFactory.Create())
        .WithTools<DataTools>(JsonOptionsFactory.Create())
        .WithTools<AdminTools>(JsonOptionsFactory.Create());

    var app = builder.Build();
    var options = app.Services.GetRequiredService<IOptions<SqlServerMcpOptions>>().Value;

    app.MapGet("/", () => Results.Ok(new
    {
        server = "SqlServerMcp",
        transport = "http",
        endpoint = options.HttpPath,
        skipDmlConfirmation = options.SkipDmlConfirmation
    }));

    app.MapMcp(options.HttpPath);
    await app.RunAsync();
    return 0;
}

static void ConfigureSharedServices(IServiceCollection services, IConfiguration configuration)
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

internal enum TransportMode
{
    Http,
    Stdio
}

internal static class StartupArguments
{
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
    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
        return options;
    }
}
