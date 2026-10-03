using AlertaRio.Worker;
using AlertaRio.Infrastructure.Ingestion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

var builder = Host.CreateApplicationBuilder(args);
if (builder.Environment.IsDevelopment() &&
    builder.Configuration.GetValue<bool>("SyntheticIngestion:Enabled"))
{
    var connectionString = builder.Configuration.GetConnectionString("Ingestion")
        ?? throw new InvalidOperationException("Ingestion connection string is required.");
    builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
    builder.Services.AddSingleton<PostgresIngestionStore>();
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddHostedService<SyntheticIngestionWorker>();
}
else
{
    builder.Services.AddHostedService<IdleWorker>();
}
await builder.Build().RunAsync();
