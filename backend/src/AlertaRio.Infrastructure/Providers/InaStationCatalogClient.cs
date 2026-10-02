using System.Text.Json;

namespace AlertaRio.Infrastructure.Providers;

// Not registered in Api or Worker until INA rights and a request budget are approved.
public sealed class InaStationCatalogClient(HttpClient client)
{
    private static readonly Uri OfficialBase = new("https://alerta.ina.gob.ar/a5/");
    private const int PageSize = 30;
    private const int MaxPages = 10;

    public async Task<IReadOnlyList<InaStationCandidate>> FetchAsync(
        string networkTable, string river, CancellationToken cancellationToken = default)
    {
        if (client.BaseAddress != OfficialBase)
            throw new InvalidOperationException("INA client must use the official A5 base URL.");
        if (string.IsNullOrWhiteSpace(networkTable) || networkTable.Length > 64 ||
            string.IsNullOrWhiteSpace(river) || river.Length > 64)
            throw new ArgumentException("Network table and river must be bounded names.");

        var candidates = new List<InaStationCandidate>();
        var identities = new HashSet<(int NetworkId, int ExternalId)>();
        var offset = 0;
        for (var pageNumber = 0; pageNumber < MaxPages; pageNumber++)
        {
            var relative = $"obs/puntual/estaciones?tabla={Uri.EscapeDataString(networkTable)}" +
                $"&rio={Uri.EscapeDataString(river)}&pagination=true&limit={PageSize}&offset={offset}";
            var uri = new Uri(OfficialBase, relative);
            var json = await ProviderJsonTransport.GetAsync(client, uri, cancellationToken);
            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object ||
                    !root.TryGetProperty("estaciones", out var rows) ||
                    rows.ValueKind != JsonValueKind.Array ||
                    !root.TryGetProperty("is_last_page", out var lastPage) ||
                    lastPage.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    throw new JsonException("INA station page is missing pagination fields.");

                var count = rows.GetArrayLength();
                if (count > PageSize || count == 0)
                    throw new ProviderFetchException(ProviderFetchFailure.IncompleteCatalog,
                        "INA station pagination is inconsistent.");
                foreach (var candidate in InaStationCandidateParser.Parse(json))
                {
                    if (!identities.Add((candidate.NetworkId, candidate.ExternalId)))
                        throw new ProviderFetchException(ProviderFetchFailure.IncompleteCatalog,
                            "INA catalog repeats a station across pages.");
                    candidates.Add(candidate);
                }
                if (lastPage.ValueKind == JsonValueKind.True)
                {
                    if (candidates.Count == 0)
                        throw new ProviderFetchException(ProviderFetchFailure.IncompleteCatalog,
                            "INA station catalog has no eligible stations.");
                    return candidates.AsReadOnly();
                }
                offset += count;
            }
            catch (JsonException exception)
            {
                throw new ProviderFetchException(ProviderFetchFailure.SchemaMismatch,
                    "INA station catalog schema changed.", innerException: exception);
            }
        }

        throw new ProviderFetchException(ProviderFetchFailure.IncompleteCatalog,
            "INA station catalog exceeded the page limit.");
    }
}
