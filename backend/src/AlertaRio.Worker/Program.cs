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
if (syntheticEnabled && inaEnabled)
    throw new InvalidOperationException("Synthetic and INA ingestion cannot run together.");
if (syntheticEnabled)
{
    var connectionString = builder.Configuration.GetConnectionString("Ingestion")
        ?? throw new InvalidOperationException("Ingestion connection string is required.");
    builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
    builder.Services.AddSingleton<PostgresIngestionStore>();
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddHostedService<SyntheticIngestionWorker>();
}
else if (inaEnabled)
{
    var options = InaPollingOptions.Read(builder.Configuration);
    var connectionString = builder.Configuration.GetConnectionString("Ingestion")
        ?? throw new InvalidOperationException("Ingestion connection string is required.");
    builder.Services.AddSingleton(options);
    builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
    builder.Services.AddSingleton<PostgresIngestionStore>();
    builder.Services.AddSingleton(TimeProvider.System);
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
else
{
    builder.Services.AddHostedService<IdleWorker>();
}
await builder.Build().RunAsync();
