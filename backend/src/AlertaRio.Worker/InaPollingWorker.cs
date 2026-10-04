using System.Globalization;
using AlertaRio.Infrastructure.Ingestion;
using AlertaRio.Infrastructure.Providers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlertaRio.Worker;

// Opt-in collector. The DB publication gates remain closed until a separate
// hydrological decision approves the series for use in citizen-facing views.
public sealed class InaPollingWorker(
    InaPollingOptions options, InaSeriesCatalogClient catalog,
    InaObservationClient observations, PostgresIngestionStore store,
    TimeProvider clock, ILogger<InaPollingWorker> logger) : BackgroundService
{
    private readonly string owner = $"ina-worker-{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = options.PollInterval;
            try
            {
                if (await PollOnceAsync(stoppingToken))
                    delay = TimeSpan.FromHours(1);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "INA collection failed; checkpoint was not advanced.");
                delay = TimeSpan.FromHours(1);
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

    private async Task<bool> PollOnceAsync(CancellationToken cancellationToken)
    {
        var streamKey = options.InternalSeriesId.ToString("D");
        if (!await store.CanCollectInaSeriesAsync(options.InternalSeriesId,
                options.Selection, cancellationToken))
            throw new InvalidOperationException(
                "INA collection is not approved in the local source and series registry.");
        if (!await store.ClaimLeaseAsync("ina", streamKey, owner,
                TimeSpan.FromMinutes(5), cancellationToken))
        {
            logger.LogInformation("INA series lease is owned by another worker.");
            return false;
        }
        var checkpoint = await store.ReadCheckpointAsync("ina", streamKey,
            cancellationToken);
        var window = InaPollingWindow.Plan(checkpoint, clock.GetUtcNow());
        var candidates = await catalog.FetchAsync(options.Selection.ExternalStationId,
            cancellationToken);
        var candidate = candidates.SingleOrDefault(item =>
            item.ExternalSeriesId == options.Selection.ExternalSeriesId)
            ?? throw new InvalidDataException("INA approved selection disappeared from catalog.");
        var permitted = InaCollectionGate.Select(candidate, options.Selection);
        var receivedAt = clock.GetUtcNow().ToUniversalTime();
        var results = await observations.FetchAsync(permitted, window.From,
            window.To, receivedAt, cancellationToken);
        if (!await store.CanCollectInaSeriesAsync(options.InternalSeriesId,
                options.Selection, cancellationToken))
            throw new InvalidOperationException(
                "INA collection rights changed before the batch was committed.");
        var cursor = window.To.ToString("O", CultureInfo.InvariantCulture);
        var batch = InaObservationBatchMapper.Map(permitted,
            options.InternalSeriesId, results, streamKey, owner, cursor, receivedAt);
        var committed = await store.CommitAsync(batch, cancellationToken);
        logger.LogInformation(
            "INA collection committed: {Inserted} inserted, {Unchanged} unchanged, " +
            "{Revised} revised, {Quarantined} quarantined; catchingUp={CatchingUp}.",
            committed.Inserted, committed.Unchanged, committed.Revised,
            committed.Quarantined, window.CatchingUp);
        return window.CatchingUp;
    }
}
