using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace AlertaRio.Infrastructure.Ingestion;

public sealed record IngestionRecord(
    Guid SeriesId, DateTimeOffset ObservedStartAt,
    DateTimeOffset ObservedEndAt, decimal? Value,
    DateTimeOffset? SourceUpdatedAt, string? SourceRecordId,
    string PayloadHash);

public sealed record RejectedIngestionRecord(
    string? SourceRecordId, string Reason, string PayloadHash);

public sealed record IngestionBatch(
    string Provider, string StreamKey, string LeaseOwner, string? Cursor,
    string Coverage, DateTimeOffset TransportSucceededAt,
    IReadOnlyList<IngestionRecord> Records,
    IReadOnlyList<RejectedIngestionRecord>? RejectedRecords = null);

public sealed record IngestionBatchResult(
    int Inserted, int Unchanged, int Revised, int Quarantined);

public sealed record IngestionCheckpoint(
    string? Cursor, string Coverage, long Version,
    DateTimeOffset? LeaseUntil);

// The SQL function applies the whole batch and its checkpoint in one transaction.
public sealed class PostgresIngestionStore(NpgsqlDataSource dataSource)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Meter Meter = new("AlertaRio.Ingestion");
    private static readonly Counter<long> SuccessfulBatches =
        Meter.CreateCounter<long>("ingestion_batches_success_total");
    private static readonly Counter<long> FailedBatches =
        Meter.CreateCounter<long>("ingestion_batches_failed_total");
    private static readonly Counter<long> InsertedRecords =
        Meter.CreateCounter<long>("ingestion_records_inserted_total");
    private static readonly Counter<long> RevisedRecords =
        Meter.CreateCounter<long>("ingestion_records_revised_total");
    private static readonly Counter<long> QuarantinedRecords =
        Meter.CreateCounter<long>("ingestion_records_quarantined_total");
    private static readonly Histogram<double> BatchDuration =
        Meter.CreateHistogram<double>("ingestion_batch_duration_seconds", "s");

    public async Task<bool> ClaimLeaseAsync(
        string provider, string streamKey, string owner, TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        if (duration <= TimeSpan.Zero || duration > TimeSpan.FromHours(1))
            throw new ArgumentOutOfRangeException(nameof(duration));
        await using var command = dataSource.CreateCommand(
            "SELECT claim_ingestion_lease($1, $2, $3, $4)");
        command.Parameters.Add(new NpgsqlParameter { Value = provider });
        command.Parameters.Add(new NpgsqlParameter { Value = streamKey });
        command.Parameters.Add(new NpgsqlParameter { Value = owner });
        command.Parameters.Add(new NpgsqlParameter { Value = duration, NpgsqlDbType = NpgsqlDbType.Interval });
        return (bool)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidDataException("Lease result was null."));
    }

    public async Task<bool> RenewLeaseAsync(
        string provider, string streamKey, string owner, TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        if (duration <= TimeSpan.Zero || duration > TimeSpan.FromHours(1))
            throw new ArgumentOutOfRangeException(nameof(duration));
        await using var command = dataSource.CreateCommand(
            "SELECT renew_ingestion_lease($1, $2, $3, $4)");
        command.Parameters.Add(new NpgsqlParameter { Value = provider });
        command.Parameters.Add(new NpgsqlParameter { Value = streamKey });
        command.Parameters.Add(new NpgsqlParameter { Value = owner });
        command.Parameters.Add(new NpgsqlParameter
        {
            Value = duration,
            NpgsqlDbType = NpgsqlDbType.Interval
        });
        return (bool)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidDataException("Lease renewal result was null."));
    }

    public async Task<IngestionBatchResult> CommitAsync(
        IngestionBatch batch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(batch.Records);
        if (batch.TransportSucceededAt.Offset != TimeSpan.Zero ||
            batch.Records.Any(record => record.ObservedStartAt.Offset != TimeSpan.Zero ||
                record.ObservedEndAt.Offset != TimeSpan.Zero ||
                record.SourceUpdatedAt is { Offset: var offset } && offset != TimeSpan.Zero))
            throw new ArgumentException("Ingestion timestamps must be UTC.", nameof(batch));

        var started = Stopwatch.GetTimestamp();
        try
        {
            await using var command = dataSource.CreateCommand(
                "SELECT inserted, unchanged, revised, quarantined " +
                "FROM commit_ingestion_outcomes($1, $2, $3, $4, $5, $6, $7, $8)");
            command.Parameters.Add(new NpgsqlParameter { Value = batch.Provider });
            command.Parameters.Add(new NpgsqlParameter { Value = batch.StreamKey });
            command.Parameters.Add(new NpgsqlParameter { Value = batch.LeaseOwner });
            command.Parameters.Add(new NpgsqlParameter
            {
                Value = (object?)batch.Cursor ?? DBNull.Value,
                NpgsqlDbType = NpgsqlDbType.Text
            });
            command.Parameters.Add(new NpgsqlParameter { Value = batch.Coverage });
            command.Parameters.Add(new NpgsqlParameter
            {
                Value = batch.TransportSucceededAt,
                NpgsqlDbType = NpgsqlDbType.TimestampTz
            });
            command.Parameters.Add(new NpgsqlParameter
            {
                Value = JsonSerializer.Serialize(batch.Records, JsonOptions),
                NpgsqlDbType = NpgsqlDbType.Jsonb
            });
            command.Parameters.Add(new NpgsqlParameter
            {
                Value = JsonSerializer.Serialize(batch.RejectedRecords ?? [], JsonOptions),
                NpgsqlDbType = NpgsqlDbType.Jsonb
            });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidDataException("Ingestion function returned no result.");
            var result = new IngestionBatchResult(reader.GetInt32(0), reader.GetInt32(1),
                reader.GetInt32(2), reader.GetInt32(3));
            SuccessfulBatches.Add(1);
            InsertedRecords.Add(result.Inserted);
            RevisedRecords.Add(result.Revised);
            QuarantinedRecords.Add(result.Quarantined);
            return result;
        }
        catch
        {
            FailedBatches.Add(1);
            throw;
        }
        finally
        {
            BatchDuration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds);
        }
    }

    public async Task<IngestionCheckpoint?> ReadCheckpointAsync(
        string provider, string streamKey, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT cursor, coverage, version, lease_until " +
            "FROM ingestion_checkpoints WHERE provider = $1 AND stream_key = $2");
        command.Parameters.Add(new NpgsqlParameter { Value = provider });
        command.Parameters.Add(new NpgsqlParameter { Value = streamKey });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new IngestionCheckpoint(reader.IsDBNull(0) ? null : reader.GetString(0),
            reader.GetString(1), reader.GetInt64(2),
            reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3));
    }
}
