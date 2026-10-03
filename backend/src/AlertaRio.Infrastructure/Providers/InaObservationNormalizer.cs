using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AlertaRio.Infrastructure.Providers;

public sealed record InaSeriesContext(
    int ExternalSeriesId, int? UnitId, string Unit, bool IsObserved, bool IsInstantaneous);

public enum InaObservationStatus
{
    Candidate,
    Missing,
    Quarantined
}

public sealed record InaObservationCandidate(
    long ExternalObservationId, int ExternalSeriesId, decimal Value, string OriginalValue,
    string Unit, DateTimeOffset ObservedStartAt, DateTimeOffset? ObservedEndAt,
    DateTimeOffset? SourceUpdatedAt, DateTimeOffset IngestedAt);

public sealed record InaMissingObservation(
    long ExternalObservationId, int ExternalSeriesId,
    DateTimeOffset ObservedStartAt, DateTimeOffset? ObservedEndAt,
    DateTimeOffset? SourceUpdatedAt);

public sealed record InaObservationResult(
    long? ExternalObservationId, InaObservationStatus Status, string? Reason,
    InaObservationCandidate? Candidate, InaMissingObservation? Missing = null,
    string? RawPayloadHash = null);

// Produces unapproved candidates from synthetic or permitted payloads; it never publishes data.
public static class InaObservationNormalizer
{
    private static readonly Regex ExplicitOffset = new(
        @"(?:Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static IReadOnlyList<InaObservationResult> Normalize(
        string json, InaSeriesContext series, DateTimeOffset ingestedAt)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var rows = root.ValueKind switch
        {
            JsonValueKind.Array => root,
            JsonValueKind.Object when root.TryGetProperty("rows", out var wrappedRows)
                && wrappedRows.ValueKind == JsonValueKind.Array => wrappedRows,
            _ => throw new JsonException("Unexpected INA observation wrapper.")
        };

        var results = new List<InaObservationResult>();
        var seenIds = new HashSet<long>();
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object)
            {
                results.Add(Quarantine(null, "invalidRow", row));
                continue;
            }

            var id = ReadLong(row, "id");
            if (id is null)
            {
                results.Add(Quarantine(null, "missingObservationId", row));
                continue;
            }
            if (!seenIds.Add(id.Value))
            {
                results.Add(Quarantine(id, "duplicateObservationId", row));
                continue;
            }
            if (!series.IsObserved || !series.IsInstantaneous)
            {
                results.Add(Quarantine(id, "unsupportedSeriesKind", row));
                continue;
            }
            if (ReadInt(row, "series_id") != series.ExternalSeriesId)
            {
                results.Add(Quarantine(id, "seriesMismatch", row));
                continue;
            }
            if (series.UnitId is null || string.IsNullOrWhiteSpace(series.Unit))
            {
                results.Add(Quarantine(id, "seriesUnitUnverified", row));
                continue;
            }
            if (!row.TryGetProperty("unit_id", out var unit) ||
                (unit.ValueKind != JsonValueKind.Null &&
                 (unit.ValueKind != JsonValueKind.Number ||
                  !unit.TryGetInt32(out var unitId) || unitId != series.UnitId)))
            {
                results.Add(Quarantine(id, "unitConflict", row));
                continue;
            }
            if (!TryReadTime(row, "timestart", required: true, out var observedStart))
            {
                results.Add(Quarantine(id, "ambiguousObservedTime", row));
                continue;
            }
            if (!TryReadTime(row, "timeend", required: false, out var observedEnd) ||
                observedEnd is not null && observedEnd < observedStart)
            {
                results.Add(Quarantine(id, "invalidObservedInterval", row));
                continue;
            }
            if (!TryReadTime(row, "timeupdate", required: false, out var sourceUpdated))
            {
                results.Add(Quarantine(id, "ambiguousSourceUpdate", row));
                continue;
            }
            if (observedStart > ingestedAt)
            {
                results.Add(Quarantine(id, "futureObservation", row));
                continue;
            }
            if (!row.TryGetProperty("valor", out var rawValue) ||
                rawValue.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
                rawValue.ValueKind == JsonValueKind.String &&
                string.Equals(rawValue.GetString(), "null", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new InaObservationResult(id, InaObservationStatus.Missing,
                    "missingValue", null,
                    new InaMissingObservation(id.Value, series.ExternalSeriesId,
                        observedStart!.Value.ToUniversalTime(), observedEnd?.ToUniversalTime(),
                        sourceUpdated?.ToUniversalTime())));
                continue;
            }
            if (rawValue.ValueKind != JsonValueKind.Number ||
                !rawValue.TryGetDecimal(out var value))
            {
                results.Add(Quarantine(id, "invalidValue", row));
                continue;
            }

            var candidate = new InaObservationCandidate(
                id.Value, series.ExternalSeriesId, value, rawValue.GetRawText(), series.Unit,
                observedStart!.Value.ToUniversalTime(), observedEnd?.ToUniversalTime(),
                sourceUpdated?.ToUniversalTime(), ingestedAt.ToUniversalTime());
            results.Add(new InaObservationResult(id, InaObservationStatus.Candidate, null, candidate));
        }

        return results;
    }

    private static int? ReadInt(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed)
            ? parsed : null;

    private static long? ReadLong(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var parsed)
            ? parsed : null;

    private static bool TryReadTime(
        JsonElement row, string name, bool required, out DateTimeOffset? timestamp)
    {
        timestamp = null;
        if (!row.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            return !required;
        if (value.ValueKind != JsonValueKind.String)
            return false;
        var raw = value.GetString();
        if (raw is null || !ExplicitOffset.IsMatch(raw) ||
            !DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed))
            return false;
        timestamp = parsed;
        return true;
    }

    private static InaObservationResult Quarantine(long? id, string reason, JsonElement row) =>
        new(id, InaObservationStatus.Quarantined, reason, null, null,
            Convert.ToHexStringLower(SHA256.HashData(
                Encoding.UTF8.GetBytes(row.GetRawText()))));
}
