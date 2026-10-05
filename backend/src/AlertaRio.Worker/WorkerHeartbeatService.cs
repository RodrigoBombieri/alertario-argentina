using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AlertaRio.Worker;

public sealed class WorkerHeartbeatService(
    NpgsqlDataSource dataSource, TimeProvider clock,
    ILogger<WorkerHeartbeatService> logger) : BackgroundService
{
    private readonly string instanceId = Guid.NewGuid().ToString("N");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var command = dataSource.CreateCommand("""
                    INSERT INTO worker_heartbeats (instance_id, last_seen_at)
                    VALUES ($1, $2)
                    ON CONFLICT (instance_id) DO UPDATE
                    SET last_seen_at = EXCLUDED.last_seen_at, updated_at = now()
                    """);
                command.Parameters.Add(new NpgsqlParameter { Value = instanceId });
                command.Parameters.Add(new NpgsqlParameter { Value = clock.GetUtcNow() });
                await command.ExecuteNonQueryAsync(stoppingToken);
                await using var cleanup = dataSource.CreateCommand(
                    "DELETE FROM worker_heartbeats WHERE last_seen_at < now() - interval '7 days'");
                await cleanup.ExecuteNonQueryAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Worker heartbeat write failed.");
            }
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
