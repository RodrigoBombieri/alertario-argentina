using System.Text.Json;

namespace AlertaRio.Infrastructure.Providers;

// Not registered in Api or Worker until INA rights and a request budget are approved.
public sealed class InaSeriesCatalogClient(HttpClient client)
{
    private static readonly Uri OfficialBase = new("https://alerta.ina.gob.ar/a5/");
    private const int MaxSeries = 300;

    public async Task<IReadOnlyList<InaSeriesCandidate>> FetchAsync(
        int stationId, CancellationToken cancellationToken = default)
    {
        if (client.BaseAddress != OfficialBase)
            throw new InvalidOperationException("INA client must use the official A5 base URL.");
        if (stationId <= 0)
            throw new ArgumentOutOfRangeException(nameof(stationId));

        var uri = new Uri(OfficialBase, $"obs/puntual/series?estacion_id={stationId}");
        var json = await ProviderJsonTransport.GetAsync(client, uri, cancellationToken);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var rows = root.ValueKind switch
            {
                JsonValueKind.Array => root,
                JsonValueKind.Object when root.TryGetProperty("rows", out var wrappedRows)
                    && wrappedRows.ValueKind == JsonValueKind.Array => wrappedRows,
                _ => throw new JsonException("Unexpected INA series wrapper.")
            };
            if (rows.GetArrayLength() >= MaxSeries || IsIncomplete(root, rows.GetArrayLength()))
                throw new ProviderFetchException(ProviderFetchFailure.IncompleteCatalog,
                    "INA series response may be truncated.");

            var candidates = InaSeriesCandidateParser.Parse(json);
            if (candidates.Any(candidate => candidate.ExternalStationId != stationId))
                throw new ProviderFetchException(ProviderFetchFailure.SchemaMismatch,
                    "INA series response contains another station.");
            return candidates;
        }
        catch (JsonException exception)
        {
            throw new ProviderFetchException(ProviderFetchFailure.SchemaMismatch,
                "INA series catalog schema changed.", innerException: exception);
        }
    }

    private static bool IsIncomplete(JsonElement root, int count)
    {
        if (root.ValueKind != JsonValueKind.Object) return false;
        if (root.TryGetProperty("is_last_page", out var lastPage) &&
            lastPage.ValueKind == JsonValueKind.False) return true;
        if (root.TryGetProperty("next_page", out var nextPage) &&
            nextPage.ValueKind != JsonValueKind.Null) return true;
        if (root.TryGetProperty("total", out var total) &&
            (total.ValueKind != JsonValueKind.Number ||
             !total.TryGetInt32(out var expected) || expected != count)) return true;
        return false;
    }
}
