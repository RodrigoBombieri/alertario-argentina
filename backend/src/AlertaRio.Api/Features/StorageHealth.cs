using Npgsql;

namespace AlertaRio.Api.Features;

internal static class StorageHealth
{
    private static readonly string[] ExpectedMigrations =
    [
        "0001_initial",
        "0002_ingestion_writer",
        "0003_trend_policy",
        "0004_normalization_outcomes",
        "0005_lease_renewal",
        "0006_station_map_index",
        "0007_quarantine_review",
        "0008_notification_outbox",
        "0009_outbox_revalidation",
        "0010_cancel_unversioned_episodes"
    ];

    internal static async Task<string> CheckAsync(
        NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            await using var command = dataSource.CreateCommand(
                "SELECT version FROM schema_migrations ORDER BY version");
            command.CommandTimeout = 3;
            await using var reader = await command.ExecuteReaderAsync(timeout.Token);
            var actual = new List<string>();
            while (await reader.ReadAsync(timeout.Token))
                actual.Add(reader.GetString(0));
            return actual.SequenceEqual(ExpectedMigrations)
                ? "ready"
                : "schemaIncompatible";
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return "storageUnavailable";
        }
        catch (PostgresException exception) when (exception.SqlState is "42P01" or "42703")
        {
            return "schemaIncompatible";
        }
        catch (InvalidCastException)
        {
            return "schemaIncompatible";
        }
        catch (NpgsqlException)
        {
            return "storageUnavailable";
        }
    }
}
