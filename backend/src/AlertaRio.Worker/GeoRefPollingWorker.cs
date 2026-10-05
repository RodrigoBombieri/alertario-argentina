using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlertaRio.Infrastructure.Ingestion;
using AlertaRio.Infrastructure.Providers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlertaRio.Worker;

public sealed class GeoRefPollingWorker(
    GeoRefPollingOptions options, GeoRefCatalogClient catalog,
    PostgresGeoRefCatalogStore store, ILogger<GeoRefPollingWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = options.PollInterval;
            try
            {
                if (!await store.CanImportAsync(options.SourceId,
                        options.RightsDecisionId, stoppingToken))
                    throw new InvalidOperationException(
                        "GeoRef import is not approved in the source registry.");
                var snapshot = await catalog.FetchAsync(stoppingToken);
                var serialized = JsonSerializer.Serialize(snapshot);
                var digest = Convert.ToHexString(SHA256.HashData(
                    Encoding.UTF8.GetBytes(serialized))).ToLowerInvariant();
                var version = $"georef-v2-{digest[..32]}";
                var imported = await store.ImportAsync(options.SourceId,
                    options.RightsDecisionId, version, snapshot, stoppingToken);
                logger.LogInformation(
                    "GeoRef catalog {Version}: {Count} localities, new={Imported}. " +
                    "A new version requires reviewed associations before activation.",
                    version, snapshot.Total, imported);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "GeoRef import failed; active catalog is unchanged.");
                delay = TimeSpan.FromHours(24);
            }
            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
