using System.Text.Json;

namespace AlertaRio.Infrastructure.Providers;

public sealed record InaStationCandidate(int ExternalId, string Name, string RiverName);

// Parses documented catalog wrappers; candidates are not approved for publication.
public static class InaStationCandidateParser
{
    public static IReadOnlyList<InaStationCandidate> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var rows = root.ValueKind switch
        {
            JsonValueKind.Array => root,
            JsonValueKind.Object when root.TryGetProperty("rows", out var wrappedRows)
                && wrappedRows.ValueKind == JsonValueKind.Array => wrappedRows,
            _ => throw new JsonException("Unexpected INA station catalog wrapper.")
        };

        var candidates = new List<InaStationCandidate>();
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object)
                throw new JsonException("Invalid INA station catalog row.");
            if (!row.TryGetProperty("public", out var visibility) ||
                visibility.ValueKind != JsonValueKind.True)
                continue;

            if (!row.TryGetProperty("id", out var id) ||
                id.ValueKind != JsonValueKind.Number || !id.TryGetInt32(out var externalId) ||
                !row.TryGetProperty("nombre", out var name) ||
                name.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(name.GetString()) ||
                !row.TryGetProperty("rio", out var river) ||
                river.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(river.GetString()))
                throw new JsonException("Public INA station row lacks required catalog fields.");

            candidates.Add(new InaStationCandidate(externalId, name.GetString()!, river.GetString()!));
        }

        return candidates;
    }
}
