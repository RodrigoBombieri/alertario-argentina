using AlertaRio.Worker;
using AlertaRio.Infrastructure.Ingestion;
using AlertaRio.Infrastructure.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

var builder = Host.CreateApplicationBuilder(args);
var syntheticEnabled = builder.Environment.IsDevelopment() &&
    builder.Configuration.GetValue<bool>("SyntheticIngestion:Enabled");
var inaEnabled = builder.Configuration.GetValue<bool>("InaIngestion:Enabled");
var geoRefEnabled = builder.Configuration.GetValue<bool>("GeoRefIngestion:Enabled");
if (syntheticEnabled && (inaEnabled || geoRefEnabled))
    throw new InvalidOperationException(
        "Synthetic and official ingestion cannot run together.");
var ingestionConnection = builder.Configuration.GetConnectionString("Ingestion");
if (syntheticEnabled || inaEnabled || geoRefEnabled)
{
    if (ingestionConnection is null)
        throw new InvalidOperationException("Ingestion connection string is required.");
}
if (ingestionConnection is not null)
{
    builder.Services.AddSingleton(NpgsqlDataSource.Create(ingestionConnection));
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddHostedService<WorkerHeartbeatService>();
}
if (syntheticEnabled)
{
    builder.Services.AddSingleton<PostgresIngestionStore>();
    builder.Services.AddHostedService<SyntheticIngestionWorker>();
}
if (inaEnabled)
{
    var options = InaPollingOptions.Read(builder.Configuration);
    builder.Services.AddSingleton(options);
    builder.Services.AddSingleton<PostgresIngestionStore>();
    builder.Services.AddHttpClient<InaSeriesCatalogClient>(client =>
        client.BaseAddress = new Uri("https://alerta.ina.gob.ar/a5/"))
        .ConfigurePrimaryHttpMessageHandler(() =>
            new HttpClientHandler { AllowAutoRedirect = false });
    builder.Services.AddHttpClient<InaObservationClient>(client =>
        client.BaseAddress = new Uri("https://alerta.ina.gob.ar/a5/"))
        .ConfigurePrimaryHttpMessageHandler(() =>
            new HttpClientHandler { AllowAutoRedirect = false });
    builder.Services.AddHostedService<InaPollingWorker>();
}
if (geoRefEnabled)
{
    builder.Services.AddSingleton(GeoRefPollingOptions.Read(builder.Configuration));
    builder.Services.AddSingleton<PostgresGeoRefCatalogStore>();
    builder.Services.AddHttpClient<GeoRefCatalogClient>(client =>
        client.BaseAddress = new Uri("https://apis.datos.gob.ar/georef/api/v2.0/"))
        .ConfigurePrimaryHttpMessageHandler(() =>
            new HttpClientHandler { AllowAutoRedirect = false });
    builder.Services.AddHostedService<GeoRefPollingWorker>();
}
if (!syntheticEnabled && !inaEnabled && !geoRefEnabled)
{
    builder.Services.AddHostedService<IdleWorker>();
}
await builder.Build().RunAsync();
