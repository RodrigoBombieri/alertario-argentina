using System.Globalization;
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
    int ExternalObservationId, int ExternalSeriesId, decimal Value, string OriginalValue,
    string Unit, DateTimeOffset ObservedStartAt, DateTimeOffset? ObservedEndAt,
    DateTimeOffset? SourceUpdatedAt, DateTimeOffset IngestedAt);

public sealed record InaObservationResult(
    int? ExternalObservationId, InaObservationStatus Status, string? Reason,
    InaObservationCandidate? Candidate);

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
        var seenIds = new HashSet<int>();
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object)
            {
                results.Add(Quarantine(null, "invalidRow"));
                continue;
            }

            var id = ReadInt(row, "id");
            if (id is null)
            {
                results.Add(Quarantine(null, "missingObservationId"));
                continue;
            }
            if (!seenIds.Add(id.Value))
            {
                results.Add(Quarantine(id, "duplicateObservationId"));
                continue;
            }
            if (!series.IsObserved || !series.IsInstantaneous)
            {
                results.Add(Quarantine(id, "unsupportedSeriesKind"));
                continue;
            }
            if (ReadInt(row, "series_id") != series.ExternalSeriesId)
            {
                results.Add(Quarantine(id, "seriesMismatch"));
                continue;
            }
            if (series.UnitId is null || string.IsNullOrWhiteSpace(series.Unit))
            {
                results.Add(Quarantine(id, "seriesUnitUnverified"));
                continue;
            }
            if (!row.TryGetProperty("unit_id", out var unit) ||
                (unit.ValueKind != JsonValueKind.Null &&
                 (unit.ValueKind != JsonValueKind.Number ||
                  !unit.TryGetInt32(out var unitId) || unitId != series.UnitId)))
            {
                results.Add(Quarantine(id, "unitConflict"));
                continue;
            }
            if (!TryReadTime(row, "timestart", required: true, out var observedStart))
            {
                results.Add(Quarantine(id, "ambiguousObservedTime"));
                continue;
            }
            if (!TryReadTime(row, "timeend", required: false, out var observedEnd) ||
                observedEnd is not null && observedEnd < observedStart)
            {
                results.Add(Quarantine(id, "invalidObservedInterval"));
                continue;
            }
            if (!TryReadTime(row, "timeupdate", required: false, out var sourceUpdated))
            {
                results.Add(Quarantine(id, "ambiguousSourceUpdate"));
                continue;
            }
            if (observedStart > ingestedAt)
            {
                results.Add(Quarantine(id, "futureObservation"));
                continue;
            }
            if (!row.TryGetProperty("valor", out var rawValue) ||
                rawValue.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
                rawValue.ValueKind == JsonValueKind.String &&
                string.Equals(rawValue.GetString(), "null", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new InaObservationResult(id, InaObservationStatus.Missing,
                    "missingValue", null));
                continue;
            }
            if (rawValue.ValueKind != JsonValueKind.Number ||
                !rawValue.TryGetDecimal(out var value))
            {
                results.Add(Quarantine(id, "invalidValue"));
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

    private static InaObservationResult Quarantine(int? id, string reason) =>
        new(id, InaObservationStatus.Quarantined, reason, null);
}
