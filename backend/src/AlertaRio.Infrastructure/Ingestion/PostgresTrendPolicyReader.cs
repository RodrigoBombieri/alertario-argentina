using AlertaRio.Core.Trends;
using Npgsql;

namespace AlertaRio.Infrastructure.Ingestion;

public sealed record ApprovedTrendConfiguration(
    TrendPolicy Policy, IReadOnlyList<OfficialThreshold> Thresholds,
    FollowUpRule? FollowUp = null);

public sealed class PostgresTrendPolicyReader(NpgsqlDataSource dataSource)
{
    public async Task<ApprovedTrendConfiguration?> ReadAsync(
        Guid seriesId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await ReadAsync(connection, null, seriesId, now, cancellationToken);
    }

    internal static async Task<ApprovedTrendConfiguration?> ReadAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction,
        Guid seriesId, DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (seriesId == Guid.Empty || now.Offset != TimeSpan.Zero)
            throw new ArgumentException("A series and UTC clock are required.");
        await using var policyCommand = new NpgsqlCommand("""
            SELECT p.cadence_seconds, p.allowed_lag_seconds, p.epsilon,
                   p.methodology_version, s.unit, s.datum_ref, s.epoch,
                   p.follow_up_window_hours, p.follow_up_minimum_rise
            FROM series_trend_policies AS p
            JOIN measurement_series AS s ON s.id = p.series_id
            JOIN data_sources AS d ON d.id = s.source_id
            WHERE p.series_id = $1 AND p.active AND s.approved AND
                  s.data_kind = 'observed' AND s.support_seconds = 0 AND
                  d.permission_status = 'approved' AND
                  p.datum_ref = s.datum_ref AND p.epoch = s.epoch
            """, connection, transaction);
        policyCommand.Parameters.Add(new NpgsqlParameter { Value = seriesId });
        int cadenceSeconds;
        int lagSeconds;
        decimal epsilon;
        string methodologyVersion;
        string unit;
        string datum;
        int epoch;
        FollowUpRule? followUp;
        await using (var reader = await policyCommand.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken)) return null;
            cadenceSeconds = reader.GetInt32(0);
            lagSeconds = reader.GetInt32(1);
            epsilon = reader.GetDecimal(2);
            methodologyVersion = reader.GetString(3);
            unit = reader.GetString(4);
            datum = reader.GetString(5);
            epoch = reader.GetInt32(6);
            followUp = reader.IsDBNull(7) ? null :
                new FollowUpRule(reader.GetInt32(7), reader.GetDecimal(8), true);
        }

        await using var thresholdCommand = new NpgsqlCommand("""
            SELECT id, kind, value, valid_from, valid_until, authority
            FROM official_thresholds
            WHERE series_id = $1 AND approved AND unit = $2 AND
                  datum_ref = $3 AND epoch = $4 AND valid_from <= $5 AND
                  (valid_until IS NULL OR $5 < valid_until)
            ORDER BY kind, value, id
            """, connection, transaction);
        thresholdCommand.Parameters.Add(new NpgsqlParameter { Value = seriesId });
        thresholdCommand.Parameters.Add(new NpgsqlParameter { Value = unit });
        thresholdCommand.Parameters.Add(new NpgsqlParameter { Value = datum });
        thresholdCommand.Parameters.Add(new NpgsqlParameter { Value = epoch });
        thresholdCommand.Parameters.Add(new NpgsqlParameter { Value = now });
        var thresholds = new List<OfficialThreshold>();
        await using var thresholdReader = await thresholdCommand.ExecuteReaderAsync(cancellationToken);
        while (await thresholdReader.ReadAsync(cancellationToken))
        {
            var condition = thresholdReader.GetString(1) switch
            {
                "alert" => CalculatedCondition.AboveAlertThreshold,
                "evacuation_reference" => CalculatedCondition.AboveEvacuationThreshold,
                _ => throw new InvalidDataException("Unknown threshold kind.")
            };
            thresholds.Add(new OfficialThreshold(
                thresholdReader.GetGuid(0).ToString("D"), seriesId,
                thresholdReader.GetDecimal(2), unit, datum, epoch, condition,
                thresholdReader.GetFieldValue<DateTimeOffset>(3),
                thresholdReader.IsDBNull(4) ? null :
                    thresholdReader.GetFieldValue<DateTimeOffset>(4), true,
                thresholdReader.GetString(5)));
        }
        return new ApprovedTrendConfiguration(
            new TrendPolicy(TimeSpan.FromSeconds(cadenceSeconds),
                TimeSpan.FromSeconds(lagSeconds), epsilon, methodologyVersion),
            thresholds, followUp);
    }
}
