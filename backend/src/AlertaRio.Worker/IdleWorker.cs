using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlertaRio.Worker;

public sealed class IdleWorker(ILogger<IdleWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("F2 worker ready; no provider ingestion configured.");
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // A normal host shutdown does not create a failed job.
        }
    }
}
