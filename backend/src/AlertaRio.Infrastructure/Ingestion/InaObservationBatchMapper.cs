using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AlertaRio.Infrastructure.Providers;

namespace AlertaRio.Infrastructure.Ingestion;

public static class InaObservationBatchMapper
{
    public static IngestionBatch Map(
        PermittedInaSeries approvedSeries, Guid internalSeriesId,
        IReadOnlyList<InaObservationResult> results,
        string streamKey, string leaseOwner, string? cursor,
        DateTimeOffset transportSucceededAt)
    {
        ArgumentNullException.ThrowIfNull(approvedSeries);
        ArgumentNullException.ThrowIfNull(results);
        if (internalSeriesId == Guid.Empty)
            throw new ArgumentException("An internal series is required.", nameof(internalSeriesId));
        if (!string.Equals(streamKey, internalSeriesId.ToString("D"),
            StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The stream key must identify the internal series.",
                nameof(streamKey));
        var records = new List<IngestionRecord>();
        var rejected = new List<RejectedIngestionRecord>();
        foreach (var result in results)
        {
            if (result.Status == InaObservationStatus.Quarantined)
            {
                if (result.Candidate is not null || result.Missing is not null ||
                    string.IsNullOrWhiteSpace(result.Reason) ||
                    result.RawPayloadHash is not { Length: 64 } rawHash)
                    throw new InvalidDataException("Rejected observation has invalid evidence.");
                rejected.Add(new RejectedIngestionRecord(
                    result.ExternalObservationId?.ToString(CultureInfo.InvariantCulture),
                    $"normalizer:{result.Reason}", rawHash));
                continue;
            }
            if (result.Status == InaObservationStatus.Missing)
            {
                var missing = result.Missing ?? throw new InvalidDataException(
                    "Missing observation has no identity.");
                if (result.Candidate is not null ||
                    missing.ExternalSeriesId != approvedSeries.Context.ExternalSeriesId ||
                    missing.ObservedStartAt.Offset != TimeSpan.Zero ||
                    missing.ObservedEndAt is { Offset: var missingOffset } &&
                    missingOffset != TimeSpan.Zero)
                    throw new InvalidDataException("Missing observation does not match the series.");
                records.Add(new IngestionRecord(internalSeriesId,
                    missing.ObservedStartAt,
                    missing.ObservedEndAt ?? missing.ObservedStartAt,
                    null, missing.SourceUpdatedAt,
                    missing.ExternalObservationId.ToString(CultureInfo.InvariantCulture),
                    Hash(missing.ExternalObservationId, missing.ExternalSeriesId,
                        missing.ObservedStartAt, missing.ObservedEndAt,
                        "missing", missing.SourceUpdatedAt)));
                continue;
            }
            if (result.Status != InaObservationStatus.Candidate)
                throw new InvalidDataException("Unknown observation status.");
            var candidate = result.Candidate ?? throw new InvalidDataException(
                "Candidate observation is missing its value.");
            if (candidate.ExternalSeriesId != approvedSeries.Context.ExternalSeriesId ||
                candidate.Unit != approvedSeries.Context.Unit ||
                candidate.ObservedStartAt.Offset != TimeSpan.Zero ||
                candidate.ObservedEndAt is { Offset: var offset } && offset != TimeSpan.Zero)
                throw new InvalidDataException("Observation does not match the approved series.");
            records.Add(new IngestionRecord(internalSeriesId,
                candidate.ObservedStartAt, candidate.ObservedEndAt ?? candidate.ObservedStartAt,
                candidate.Value, candidate.SourceUpdatedAt,
                candidate.ExternalObservationId.ToString(CultureInfo.InvariantCulture),
                Hash(candidate.ExternalObservationId, candidate.ExternalSeriesId,
                    candidate.ObservedStartAt, candidate.ObservedEndAt,
                    candidate.OriginalValue, candidate.SourceUpdatedAt)));
        }
        return new IngestionBatch("ina", streamKey, leaseOwner, cursor,
            results.Any(result => result.Status != InaObservationStatus.Candidate)
                ? "partial" : "complete",
            transportSucceededAt, records, rejected);
    }

    private static string Hash(
        long observationId, int seriesId, DateTimeOffset observedStartAt,
        DateTimeOffset? observedEndAt, string originalValue,
        DateTimeOffset? sourceUpdatedAt)
    {
        var canonical = string.Join('|',
            observationId.ToString(CultureInfo.InvariantCulture),
            seriesId.ToString(CultureInfo.InvariantCulture),
            observedStartAt.ToString("O", CultureInfo.InvariantCulture),
            observedEndAt?.ToString("O", CultureInfo.InvariantCulture) ?? "",
            originalValue,
            sourceUpdatedAt?.ToString("O", CultureInfo.InvariantCulture) ?? "");
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
