using Npgsql;

namespace AlertaRio.Api.Features;

internal static class DataQualityHealth
{
    internal static async Task<bool> CheckAsync(NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT NOT EXISTS (
                SELECT 1 FROM series_trend_policies policy
                JOIN measurement_series series ON series.id = policy.series_id
                JOIN data_sources source ON source.id = series.source_id
                JOIN stations station ON station.id = series.station_id
                LEFT JOIN series_latest latest ON latest.series_id = series.id
                LEFT JOIN publishable_measurements measurement ON measurement.id = latest.measurement_id
                WHERE policy.active AND series.approved AND station.active
                    AND source.permission_status = 'approved'
                    AND (measurement.id IS NULL OR measurement.observed_end_at <
                        now() - make_interval(secs => policy.cadence_seconds + policy.allowed_lag_seconds))
            ) AND NOT EXISTS (
                SELECT 1 FROM ingestion_checkpoints
                WHERE coverage <> 'complete'
            ) AND NOT EXISTS (
                SELECT 1 FROM quarantined_records WHERE review_status = 'open'
            )
            """);
        command.CommandTimeout = 3;
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }
}
