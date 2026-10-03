using AlertaRio.Application.Ports;
using AlertaRio.Application.PublicData;
using AlertaRio.Core.Trends;
using Npgsql;

namespace AlertaRio.Infrastructure.Ingestion;

// Development-only projection until catalog and notice feeds pass their gates.
public sealed class PostgresSummaryReader(
    NpgsqlDataSource dataSource, TimeProvider clock) : IPersistedSummaryReader
{
    private static readonly int[] Windows = [1, 3, 6, 12, 24];

    public async Task<StationSummaryDto?> GetSummaryAsync(
        string stationId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(stationId, out var stationGuid)) return null;
        var now = clock.GetUtcNow();
        await using var seriesCommand = dataSource.CreateCommand("""
            SELECT ms.id FROM measurement_series AS ms
            JOIN stations AS st ON st.id = ms.station_id
            JOIN data_sources AS d ON d.id = ms.source_id
            WHERE st.id = $1 AND st.active AND ms.variable_code = 'H' AND
                  ms.unit = 'm' AND ms.approved AND ms.data_kind = 'observed' AND
                  ms.support_seconds = 0 AND d.permission_status = 'approved'
            ORDER BY ms.id LIMIT 2
            """);
        seriesCommand.Parameters.Add(new NpgsqlParameter { Value = stationGuid });
        var series = new List<Guid>();
        await using (var reader = await seriesCommand.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken))
                series.Add(reader.GetGuid(0));
        if (series.Count == 0) return null;
        if (series.Count > 1)
            return Unavailable(stationId, now, "ambiguousHeightSeries");

        var seriesId = series[0];
        var configuration = await new PostgresTrendPolicyReader(dataSource)
            .ReadAsync(seriesId, now, cancellationToken);
        if (configuration is null)
            return Unavailable(stationId, now, "noApprovedPolicy");
        var trend = await new PostgresTrendReader(dataSource)
            .ReadAsync(seriesId, configuration.Policy, now, cancellationToken);
        if (trend is null)
            return Unavailable(stationId, now, "noAcceptedMeasurement");

        await using var latestCommand = dataSource.CreateCommand("""
            SELECT p.value, p.observed_end_at, p.source_updated_at,
                   p.ingested_at, p.revision, l.version, s.unit, s.datum_ref,
                   s.epoch, s.source_id
            FROM series_latest AS l
            JOIN publishable_measurements AS p
              ON p.id = l.measurement_id AND p.series_id = l.series_id
            JOIN measurement_series AS s ON s.id = l.series_id
            WHERE l.series_id = $1
            """);
        latestCommand.Parameters.Add(new NpgsqlParameter { Value = seriesId });
        decimal value;
        DateTimeOffset observedAt;
        DateTimeOffset? sourceUpdatedAt;
        DateTimeOffset ingestedAt;
        int revision;
        long version;
        string unit;
        string datum;
        int epoch;
        Guid sourceId;
        await using (var reader = await latestCommand.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
                return Unavailable(stationId, now, "latestChangedDuringRead");
            value = reader.GetDecimal(0);
            observedAt = reader.GetFieldValue<DateTimeOffset>(1);
            sourceUpdatedAt = reader.IsDBNull(2) ? null :
                reader.GetFieldValue<DateTimeOffset>(2);
            ingestedAt = reader.GetFieldValue<DateTimeOffset>(3);
            revision = reader.GetInt32(4);
            version = reader.GetInt64(5);
            unit = reader.GetString(6);
            datum = reader.IsDBNull(7) ? "" : reader.GetString(7);
            epoch = reader.GetInt32(8);
            sourceId = reader.GetGuid(9);
        }
        if (trend.LatestObservedAt != observedAt || trend.LatestValue != value ||
            trend.LatestUnit != unit || trend.LatestDatum != datum ||
            trend.LatestEpoch != epoch)
            return Unavailable(stationId, now, "dataChangedDuringRead");

        await using var qualityCommand = dataSource.CreateCommand("""
            SELECT EXISTS (
                SELECT 1 FROM ingestion_checkpoints
                WHERE stream_key = $1 AND coverage = 'complete'
            ) AND NOT EXISTS (
                SELECT 1 FROM ingestion_checkpoints
                WHERE stream_key = $1 AND coverage <> 'complete'
            ) AND NOT EXISTS (
                SELECT 1 FROM quarantined_records
                WHERE stream_key = $1 AND received_at >= $2
            )
            """);
        qualityCommand.Parameters.Add(new NpgsqlParameter { Value = seriesId.ToString("D") });
        qualityCommand.Parameters.Add(new NpgsqlParameter { Value = now.AddHours(-24) });
        var calculationApproved = (bool)(await qualityCommand.ExecuteScalarAsync(cancellationToken)
            ?? false);
        var level = new CurrentLevel(seriesId, observedAt, value, unit,
            datum, epoch, calculationApproved);
        var state = CalculatedStateEngine.Evaluate(trend, level,
            configuration.Thresholds, null, [], NoticeCoverage.Unavailable, now);
        var changes = trend.Windows.Select(window => new ChangeDto(
            window.WindowHours, window.Delta, unit, window.ReferenceAt,
            window.ActualDurationSeconds, "observedEndpoints",
            Camel(window.Direction), Camel(window.Availability),
            window.Reason)).ToArray();
        var thresholds = configuration.Thresholds.Select(threshold =>
            new ThresholdDto(threshold.ReferenceId, threshold.Value,
                threshold.Unit, threshold.Authority, threshold.Datum,
                threshold.ValidFrom, calculationApproved &&
                    state.DataStatus == DataStatus.Current
                    ? value >= threshold.Value ? "above" : "below"
                    : "unavailable")).ToArray();
        return new StationSummaryDto(stationId, now, true,
            new MeasurementDto(seriesId.ToString("D"), value, unit,
                observedAt, sourceUpdatedAt, ingestedAt, "accepted",
                Camel(trend.Freshness), sourceId.ToString("D")),
            null, "noApprovedSeries", changes, Camel(state.DataStatus),
            Camel(state.Condition),
            thresholds, [], new NoticeCoverageDto("notConfigured", null),
            configuration.Policy.MethodologyVersion, $"{version}:{revision}");
    }

    private static StationSummaryDto Unavailable(
        string stationId, DateTimeOffset now, string reason) =>
        new(stationId, now, true, null, null, "noApprovedSeries",
            Windows.Select(window => new ChangeDto(window, null, "m", null, null,
                "observedEndpoints", "unavailable", "insufficientData", reason))
                .ToArray(),
            "unavailable", "unavailable", [], [],
            new NoticeCoverageDto("notConfigured", null),
            "notApplied", "unavailable");

    private static string Camel<T>(T value) where T : Enum
    {
        var raw = value.ToString();
        return char.ToLowerInvariant(raw[0]) + raw[1..];
    }
}
