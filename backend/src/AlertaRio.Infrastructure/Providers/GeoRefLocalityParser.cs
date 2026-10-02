using System.Text.Json;

namespace AlertaRio.Infrastructure.Providers;

public sealed record GeoRefLocalityCandidate(
    string ExternalId, string Name, string Category, string ProvinceId,
    string ProvinceName, double? Latitude, double? Longitude);

public sealed record GeoRefLocalityPage(
    int Start, int Count, int Total, IReadOnlyList<GeoRefLocalityCandidate> Candidates);

// Candidates keep GeoRef identity; they do not imply an association with a river or station.
public static class GeoRefLocalityParser
{
    public static GeoRefLocalityPage Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !TryInt(root, "inicio", out var start) ||
            !TryInt(root, "cantidad", out var count) ||
            !TryInt(root, "total", out var total) ||
            !root.TryGetProperty("localidades", out var locations) ||
            locations.ValueKind != JsonValueKind.Array ||
            start < 0 || count < 0 || total < 0 ||
            count != locations.GetArrayLength() || (long)start + count > total)
            throw new JsonException("Invalid GeoRef locality page.");

        var candidates = new List<GeoRefLocalityCandidate>(count);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var location in locations.EnumerateArray())
        {
            if (location.ValueKind != JsonValueKind.Object ||
                !TryString(location, "id", out var id) ||
                !TryString(location, "nombre", out var name) ||
                !TryString(location, "categoria", out var category) ||
                !location.TryGetProperty("provincia", out var province) ||
                province.ValueKind != JsonValueKind.Object ||
                !TryString(province, "id", out var provinceId) ||
                !TryString(province, "nombre", out var provinceName) ||
                !ids.Add(id))
                throw new JsonException("Invalid or duplicate GeoRef locality identity.");

            double? latitude = null;
            double? longitude = null;
            if (location.TryGetProperty("centroide", out var centroid) &&
                centroid.ValueKind != JsonValueKind.Null)
            {
                if (centroid.ValueKind != JsonValueKind.Object ||
                    !TryDouble(centroid, "lat", out var lat) ||
                    !TryDouble(centroid, "lon", out var lon) ||
                    lat is < -90 or > 90 || lon is < -180 or > 180)
                    throw new JsonException("Invalid GeoRef locality centroid.");
                latitude = lat;
                longitude = lon;
            }

            candidates.Add(new GeoRefLocalityCandidate(
                id, name, category, provinceId, provinceName, latitude, longitude));
        }

        return new GeoRefLocalityPage(start, count, total, candidates);
    }

    private static bool TryString(JsonElement parent, string name, out string value)
    {
        value = string.Empty;
        if (!parent.TryGetProperty(name, out var field) || field.ValueKind != JsonValueKind.String)
            return false;
        value = field.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryInt(JsonElement parent, string name, out int value)
    {
        value = 0;
        return parent.TryGetProperty(name, out var field) &&
               field.ValueKind == JsonValueKind.Number && field.TryGetInt32(out value);
    }

    private static bool TryDouble(JsonElement parent, string name, out double value)
    {
        value = 0;
        return parent.TryGetProperty(name, out var field) &&
               field.ValueKind == JsonValueKind.Number && field.TryGetDouble(out value) &&
               double.IsFinite(value);
    }
}
