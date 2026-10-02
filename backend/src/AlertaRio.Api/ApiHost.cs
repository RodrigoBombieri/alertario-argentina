using AlertaRio.Api.Features;
using AlertaRio.Application.Ports;
using AlertaRio.Infrastructure.Synthetic;

namespace AlertaRio.Api;

public static class ApiHost
{
    public static WebApplication Build(string[] args) => Build(new WebApplicationOptions { Args = args });

    public static WebApplication Build(
        WebApplicationOptions options,
        Action<WebApplicationBuilder>? configure = null)
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

        var syntheticEnabled = builder.Environment.IsDevelopment()
            && builder.Configuration.GetValue<bool>("SyntheticData:Enabled");
        if (syntheticEnabled)
            builder.Services.AddSingleton<IPublicDataReader, SyntheticPublicDataReader>();
        else
            builder.Services.AddSingleton<IPublicDataReader, UnavailablePublicDataReader>();

        var app = builder.Build();
        app.UseExceptionHandler();

        if (app.Environment.IsDevelopment()) app.MapOpenApi();

        app.MapGet("/health/live", () => Results.Ok(new { status = "live" }))
            .ExcludeFromDescription();
        app.MapGet("/health/ready", (IPublicDataReader reader, HttpContext context) =>
                reader.IsConfigured
                    ? Results.Ok(new { status = "ready", synthetic = reader.IsSynthetic })
                    : ApiProblems.Create(context, StatusCodes.Status503ServiceUnavailable,
                        "dataNotConfigured", "No public data source is configured."))
            .ExcludeFromDescription();

        app.MapPublicDataEndpoints();
        return app;
    }
}
