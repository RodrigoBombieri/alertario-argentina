using AlertaRio.Api.Features;
using AlertaRio.Application.Ports;
using AlertaRio.Infrastructure.Ingestion;
using AlertaRio.Infrastructure.Synthetic;
using Npgsql;

namespace AlertaRio.Api;

public static class ApiHost
{
    public static WebApplication Build(string[] args) => Build(new WebApplicationOptions { Args = args });

    public static WebApplication Build(
        WebApplicationOptions options,
        Action<WebApplicationBuilder>? configure = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var builder = WebApplication.CreateBuilder(options);
        configure?.Invoke(builder);
        builder.Services.AddProblemDetails();
        builder.Services.AddOpenApi(options => options.AddDocumentTransformer(
            (document, _, _) =>
            {
                document.Servers?.Clear();
                return Task.CompletedTask;
            }));
        builder.Services.AddSingleton(TimeProvider.System);

        var persistedSummaryEnabled = builder.Environment.IsDevelopment()
            && builder.Configuration.GetValue<bool>("PersistedSummary:Enabled");
        var syntheticEnabled = builder.Environment.IsDevelopment()
            && builder.Configuration.GetValue<bool>("SyntheticData:Enabled");
        var collectingEnabled = builder.Configuration.GetValue<bool>("ColdStart:Enabled");
        if (persistedSummaryEnabled)
        {
            var connectionString = builder.Configuration.GetConnectionString("Ingestion")
                ?? throw new InvalidOperationException("Ingestion connection string is required.");
            builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
            builder.Services.AddSingleton<IPersistedSummaryReader, PostgresSummaryReader>();
            builder.Services.AddSingleton<IPersistedStationMapReader, PostgresStationMapReader>();
            builder.Services.AddSingleton<IPersistedHistoryReader, PostgresHistoryReader>();
            builder.Services.AddSingleton<IPublicDataReader, UnavailablePublicDataReader>();
        }
        else if (syntheticEnabled)
        {
            builder.Services.AddSingleton<IPublicDataReader, SyntheticPublicDataReader>();
            builder.Services.AddSingleton<IPersistedHistoryReader, SyntheticHistoryReader>();
        }
        else if (collectingEnabled)
        {
            builder.Services.AddSingleton<IPublicDataReader, CollectingPublicDataReader>();
            builder.Services.AddSingleton<IPersistedHistoryReader, SyntheticHistoryReader>();
        }
        else
            builder.Services.AddSingleton<IPublicDataReader, UnavailablePublicDataReader>();
        configureServices?.Invoke(builder.Services);

        var app = builder.Build();
        app.UseExceptionHandler();

        if (app.Environment.IsDevelopment()) app.MapOpenApi();

        app.MapGet("/health/live", () => Results.Ok(new { status = "live" }))
            .ExcludeFromDescription();
        app.MapGet("/health/ready", (IPublicDataReader reader, HttpContext context) =>
                reader.IsConfigured
                    ? Results.Ok(new
                    {
                        status = "ready",
                        synthetic = reader.IsSynthetic,
                        collecting = reader.IsCollecting
                    })
                    : ApiProblems.Create(context, StatusCodes.Status503ServiceUnavailable,
                        "dataNotConfigured", "No public data source is configured."))
            .ExcludeFromDescription();
        app.MapGet("/health/storage", async (IServiceProvider services, HttpContext context) =>
            {
                var dataSource = services.GetService<NpgsqlDataSource>();
                if (dataSource is null)
                    return Results.Json(new { status = "notConfigured" }, statusCode: 503);

                var status = await StorageHealth.CheckAsync(dataSource, context.RequestAborted);
                return Results.Json(new { status }, statusCode: status == "ready" ? 200 : 503);
            })
            .ExcludeFromDescription();

        app.MapPublicDataEndpoints();
        return app;
    }
}
