using AlertaRio.Core.Trends;
using Npgsql;

namespace AlertaRio.Infrastructure.Ingestion;

// Reads only rows that pass the rights/series publication gate.
public sealed class PostgresTrendReader(NpgsqlDataSource dataSource)
{
    public async Task<TrendSnapshot?> ReadAsync(
        Guid seriesId, TrendPolicy policy, DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (seriesId == Guid.Empty || now.Offset != TimeSpan.Zero)
            throw new ArgumentException("A series and UTC clock are required.");
        ArgumentNullException.ThrowIfNull(policy);
        await using var latestCommand = dataSource.CreateCommand(
            "SELECT p.observed_end_at FROM series_latest AS l " +
            "JOIN publishable_measurements AS p ON p.id = l.measurement_id " +
            "AND p.series_id = l.series_id " +
            "WHERE l.series_id = $1 AND p.observed_start_at = p.observed_end_at " +
            "AND p.observed_end_at <= $2");
        latestCommand.Parameters.Add(new NpgsqlParameter { Value = seriesId });
        latestCommand.Parameters.Add(new NpgsqlParameter { Value = now });
        var latest = await latestCommand.ExecuteScalarAsync(cancellationToken);
        if (latest is null) return null;
        var latestAt = new DateTimeOffset((DateTime)latest, TimeSpan.Zero);

        await using var command = dataSource.CreateCommand(
            "SELECT p.observed_end_at, p.value, p.revision, s.unit, " +
            "s.datum_ref, s.epoch, s.procedure_name " +
            "FROM publishable_measurements AS p " +
            "JOIN measurement_series AS s ON s.id = p.series_id " +
            "WHERE p.series_id = $1 AND p.observed_start_at = p.observed_end_at " +
            "AND p.observed_end_at BETWEEN $2 AND $3 " +
            "ORDER BY p.observed_end_at DESC LIMIT 5001");
        command.Parameters.Add(new NpgsqlParameter { Value = seriesId });
        command.Parameters.Add(new NpgsqlParameter { Value = latestAt.AddHours(-25) });
        command.Parameters.Add(new NpgsqlParameter { Value = latestAt });
        var samples = new List<TrendSample>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            samples.Add(new TrendSample(reader.GetFieldValue<DateTimeOffset>(0),
                reader.GetDecimal(1), reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetInt32(5), reader.GetString(6),
                TrendSampleQuality.Accepted, reader.GetInt32(2)));
        }
        if (samples.Count > 5000)
            throw new InvalidDataException("Trend sample window exceeds the safe read limit.");
        if (samples.Count == 0 || samples[0].ObservedAt != latestAt)
            throw new InvalidDataException("Latest measurement is missing from trend window.");
        return TrendEngine.Calculate(samples, policy, now);
    }
}
