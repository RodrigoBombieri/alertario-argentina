using System.Text.Json;

namespace AlertaRio.Infrastructure.Providers;

public sealed record InaStationCandidate(
    int ExternalId, int NetworkId, string NetworkCode, string? OwnerStationId,
    string Name, string RiverName, double Longitude, double Latitude);

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
            JsonValueKind.Object when root.TryGetProperty("estaciones", out var stations)
                && stations.ValueKind == JsonValueKind.Array => stations,
            JsonValueKind.Object when root.TryGetProperty("rows", out var wrappedRows)
                && wrappedRows.ValueKind == JsonValueKind.Array => wrappedRows,
            _ => throw new JsonException("Unexpected INA station catalog wrapper.")
        };

        var candidates = new List<InaStationCandidate>();
        var identities = new HashSet<(int NetworkId, int ExternalId)>();
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object)
                throw new JsonException("Invalid INA station catalog row.");
            if (!row.TryGetProperty("public", out var visibility) ||
                visibility.ValueKind != JsonValueKind.True ||
                !row.TryGetProperty("red", out var network) ||
                network.ValueKind != JsonValueKind.Object ||
                !network.TryGetProperty("public", out var networkVisibility) ||
                networkVisibility.ValueKind != JsonValueKind.True)
                continue;

            if (!TryInt(row, "id", out var externalId) ||
                !TryInt(network, "id", out var networkId) ||
                !TryString(row, "tabla", out var networkCode) ||
                !TryString(row, "nombre", out var name) ||
                !TryString(row, "rio", out var river) ||
                !TryOwnerStationId(row, out var ownerStationId) ||
                !TryPoint(row, out var longitude, out var latitude) ||
                !identities.Add((networkId, externalId)))
                throw new JsonException("Public INA station row lacks required catalog fields.");

            candidates.Add(new InaStationCandidate(externalId, networkId, networkCode,
                ownerStationId, name, river, longitude, latitude));
        }

        return candidates;
    }

    private static bool TryInt(JsonElement parent, string name, out int value)
    {
        value = 0;
        return parent.TryGetProperty(name, out var field) &&
               field.ValueKind == JsonValueKind.Number && field.TryGetInt32(out value);
    }

    private static bool TryString(JsonElement parent, string name, out string value)
    {
        value = string.Empty;
        if (!parent.TryGetProperty(name, out var field) || field.ValueKind != JsonValueKind.String)
            return false;
        value = field.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryOwnerStationId(JsonElement row, out string? ownerStationId)
    {
        ownerStationId = null;
        if (!row.TryGetProperty("id_externo", out var field) ||
            field.ValueKind == JsonValueKind.Null)
            return true;
        if (field.ValueKind == JsonValueKind.String)
        {
            ownerStationId = field.GetString();
            return true;
        }
        if (field.ValueKind == JsonValueKind.Number)
        {
            ownerStationId = field.GetRawText();
            return true;
        }
        return false;
    }

    private static bool TryPoint(JsonElement row, out double longitude, out double latitude)
    {
        longitude = 0;
        latitude = 0;
        if (!row.TryGetProperty("geom", out var geom) ||
            geom.ValueKind != JsonValueKind.Object ||
            !TryString(geom, "type", out var type) || type != "Point" ||
            !geom.TryGetProperty("coordinates", out var coordinates) ||
            coordinates.ValueKind != JsonValueKind.Array ||
            coordinates.GetArrayLength() is < 2 or > 3)
            return false;
        var lon = coordinates[0];
        var lat = coordinates[1];
        if (lon.ValueKind != JsonValueKind.Number || !lon.TryGetDouble(out longitude) ||
            lat.ValueKind != JsonValueKind.Number || !lat.TryGetDouble(out latitude) ||
            !double.IsFinite(longitude) || !double.IsFinite(latitude) ||
            longitude is < -180 or > 180 || latitude is < -90 or > 90)
            return false;
        if (coordinates.GetArrayLength() == 3 &&
            (coordinates[2].ValueKind != JsonValueKind.Number ||
             !coordinates[2].TryGetDouble(out var altitude) || !double.IsFinite(altitude)))
            return false;
        return true;
    }
}
