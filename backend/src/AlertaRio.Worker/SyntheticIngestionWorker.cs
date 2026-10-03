using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AlertaRio.Infrastructure.Ingestion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlertaRio.Worker;

// Explicit local one-shot path for exercising the durable writer without polling a provider.
public sealed class SyntheticIngestionWorker(
    PostgresIngestionStore store, IConfiguration configuration,
    TimeProvider clock, IHostApplicationLifetime lifetime,
    ILogger<SyntheticIngestionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var section = configuration.GetSection("SyntheticIngestion");
            if (!Guid.TryParse(section["SeriesId"], out var seriesId) || seriesId == Guid.Empty ||
                !DateTimeOffset.TryParse(section["ObservedAt"], CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var observedAt) ||
                observedAt.Offset != TimeSpan.Zero ||
                !decimal.TryParse(section["Value"], NumberStyles.Number,
                    CultureInfo.InvariantCulture, out var value))
                throw new InvalidOperationException(
                    "Synthetic ingestion requires SeriesId, UTC ObservedAt and Value.");
            var sourceUpdatedAt = observedAt;
            var hashInput = string.Join('|', seriesId.ToString("D"),
                observedAt.ToString("O", CultureInfo.InvariantCulture),
                value.ToString(CultureInfo.InvariantCulture));
            var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(hashInput)));
            var owner = $"synthetic-{Guid.NewGuid():N}";
            const string provider = "synthetic-worker";
            var streamKey = seriesId.ToString("D");
            if (!await store.ClaimLeaseAsync(provider, streamKey, owner,
                TimeSpan.FromMinutes(1), stoppingToken))
            {
                logger.LogWarning("Synthetic ingestion lease is held by another worker.");
                Environment.ExitCode = 2;
                return;
            }
            var batch = new IngestionBatch(provider, streamKey, owner,
                section["Cursor"] ?? observedAt.ToString("O", CultureInfo.InvariantCulture),
                "complete", clock.GetUtcNow(),
                [new IngestionRecord(seriesId, observedAt, observedAt, value,
                    sourceUpdatedAt, $"synthetic-{observedAt:O}", hash)]);
            var result = await store.CommitAsync(batch, stoppingToken);
            logger.LogInformation(
                "Synthetic ingestion committed: {Inserted} inserted, {Unchanged} unchanged, " +
                "{Revised} revised, {Quarantined} quarantined.",
                result.Inserted, result.Unchanged, result.Revised, result.Quarantined);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            Environment.ExitCode = 1;
            logger.LogError(exception, "Synthetic ingestion failed.");
            throw;
        }
        finally
        {
            lifetime.StopApplication();
        }
    }
}
