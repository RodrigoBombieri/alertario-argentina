using Npgsql;

namespace AlertaRio.Api.Features;

internal static class WorkerHealth
{
    internal static async Task<string> CheckAsync(
        NpgsqlDataSource dataSource, TimeProvider clock,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            await using var command = dataSource.CreateCommand(
                "SELECT max(last_seen_at) FROM worker_heartbeats");
            command.CommandTimeout = 3;
            var result = await command.ExecuteScalarAsync(timeout.Token);
            if (result is null or DBNull) return "workerUnavailable";
            var lastSeen = new DateTimeOffset(
                DateTime.SpecifyKind((DateTime)result, DateTimeKind.Utc));
            return lastSeen >= clock.GetUtcNow().AddMinutes(-2) &&
                   lastSeen <= clock.GetUtcNow().AddMinutes(1)
                ? "ready" : "workerStale";
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return "workerUnavailable";
        }
        catch (PostgresException exception) when (exception.SqlState is "42P01" or "42703")
        {
            return "schemaIncompatible";
        }
        catch (NpgsqlException)
        {
            return "workerUnavailable";
        }
    }
}
