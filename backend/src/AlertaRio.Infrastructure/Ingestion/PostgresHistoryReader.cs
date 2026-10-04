using System.Data;
using AlertaRio.Application.Ports;
using AlertaRio.Application.PublicData;
using Npgsql;

namespace AlertaRio.Infrastructure.Ingestion;

// Development preview. The graph reads accepted instantaneous observations only.
public sealed class PostgresHistoryReader(
    NpgsqlDataSource dataSource, TimeProvider clock) : IPersistedHistoryReader
{
    public async Task<SeriesHistoryPageDto?> GetPageAsync(
        string seriesId, DateTimeOffset from, DateTimeOffset to,
        DateTimeOffset? before, int limit,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(seriesId, out var id)) return null;
        if (from >= to || to - from > TimeSpan.FromDays(31) ||
            before is not null && (before <= from || before > to) ||
            limit is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(from));
        var now = clock.GetUtcNow();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.RepeatableRead, cancellationToken);
        await using (var readOnly = new NpgsqlCommand(
            "SET TRANSACTION READ ONLY", connection, transaction))
            await readOnly.ExecuteNonQueryAsync(cancellationToken);
        await using var seriesCommand = new NpgsqlCommand("""
            SELECT s.unit, s.cadence_seconds
            FROM measurement_series AS s
            JOIN data_sources AS d ON d.id = s.source_id
            WHERE s.id = $1 AND s.approved AND s.data_kind = 'observed' AND
                  s.support_seconds = 0 AND d.permission_status = 'approved'
            """, connection, transaction);
        seriesCommand.Parameters.Add(new NpgsqlParameter { Value = id });
        string unit;
        int? cadence;
        await using (var reader = await seriesCommand.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken)) return null;
            unit = reader.GetString(0);
            cadence = reader.IsDBNull(1) ? null : reader.GetInt32(1);
        }
        await using var command = new NpgsqlCommand("""
            SELECT p.observed_end_at, p.value, p.source_updated_at,
                   p.ingested_at, p.revision
            FROM publishable_measurements AS p
            WHERE p.series_id = $1 AND p.observed_start_at = p.observed_end_at AND
                  p.observed_end_at >= $2 AND p.observed_end_at < $3 AND
                  ($4::timestamptz IS NULL OR p.observed_end_at < $4)
            ORDER BY p.observed_end_at DESC LIMIT $5
            """, connection, transaction);
        command.Parameters.Add(new NpgsqlParameter { Value = id });
        command.Parameters.Add(new NpgsqlParameter { Value = from });
        command.Parameters.Add(new NpgsqlParameter { Value = to });
        command.Parameters.Add(new NpgsqlParameter
        {
            Value = before is null ? DBNull.Value : before.Value
        });
        command.Parameters.Add(new NpgsqlParameter { Value = limit + 1 });
        var points = new List<HistoryPointDto>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken))
                points.Add(new HistoryPointDto(
                    reader.GetFieldValue<DateTimeOffset>(0), reader.GetDecimal(1),
                    reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2),
                    reader.GetFieldValue<DateTimeOffset>(3), reader.GetInt32(4)));
        var hasMore = points.Count > limit;
        if (hasMore) points.RemoveAt(points.Count - 1);
        var nextCursor = hasMore ? points[^1].ObservedAt.ToUniversalTime().ToString("O") : null;
        await transaction.CommitAsync(cancellationToken);
        return new SeriesHistoryPageDto(id.ToString("D"), unit, cadence,
            now, true, from, to, nextCursor, points);
    }

    public async Task<SeriesHistoryDto?> GetRecentAsync(
        string seriesId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(seriesId, out var id)) return null;
        var now = clock.GetUtcNow();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.RepeatableRead, cancellationToken);
        await using (var readOnly = new NpgsqlCommand(
            "SET TRANSACTION READ ONLY", connection, transaction))
            await readOnly.ExecuteNonQueryAsync(cancellationToken);
        await using var seriesCommand = new NpgsqlCommand("""
            SELECT s.unit, s.cadence_seconds
            FROM measurement_series AS s
            JOIN data_sources AS d ON d.id = s.source_id
            WHERE s.id = $1 AND s.approved AND s.data_kind = 'observed' AND
                  s.support_seconds = 0 AND d.permission_status = 'approved'
            """, connection, transaction);
        seriesCommand.Parameters.Add(new NpgsqlParameter { Value = id });
        string unit;
        int? cadence;
        await using (var reader = await seriesCommand.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken)) return null;
            unit = reader.GetString(0);
            cadence = reader.IsDBNull(1) ? null : reader.GetInt32(1);
        }

        await using var command = new NpgsqlCommand("""
            SELECT p.observed_end_at, p.value, p.source_updated_at,
                   p.ingested_at, p.revision
            FROM publishable_measurements AS p
            WHERE p.series_id = $1 AND p.observed_start_at = p.observed_end_at AND
                  p.observed_end_at BETWEEN $2 AND $3
            ORDER BY p.observed_end_at DESC, p.id DESC LIMIT 2001
            """, connection, transaction);
        command.Parameters.Add(new NpgsqlParameter { Value = id });
        command.Parameters.Add(new NpgsqlParameter { Value = now.AddHours(-24) });
        command.Parameters.Add(new NpgsqlParameter { Value = now });
        var points = new List<HistoryPointDto>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken))
                points.Add(new HistoryPointDto(
                    reader.GetFieldValue<DateTimeOffset>(0), reader.GetDecimal(1),
                    reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2),
                    reader.GetFieldValue<DateTimeOffset>(3), reader.GetInt32(4)));
        var truncated = points.Count > 2000;
        if (truncated) points.RemoveAt(points.Count - 1);
        points.Reverse();
        await transaction.CommitAsync(cancellationToken);
        return new SeriesHistoryDto(id.ToString("D"), unit, cadence,
            now, true, truncated, points);
    }
}
