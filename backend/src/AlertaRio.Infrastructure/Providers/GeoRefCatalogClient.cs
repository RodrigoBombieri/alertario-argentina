using System.Text.Json;

namespace AlertaRio.Infrastructure.Providers;

// Not registered in Api or Worker until product/legal review approves catalog use.
public sealed class GeoRefCatalogClient(HttpClient client)
{
    private static readonly Uri OfficialBase = new("https://apis.datos.gob.ar/georef/api/v2.0/");
    private const int PageSize = 100;
    private const int MaxPages = 200;

    public async Task<GeoRefCatalogSnapshot> FetchAsync(
        CancellationToken cancellationToken = default)
    {
        if (client.BaseAddress != OfficialBase)
            throw new InvalidOperationException("GeoRef client must use the official v2 base URL.");

        var pages = new List<GeoRefLocalityPage>();
        var start = 0;
        for (var pageNumber = 0; pageNumber < MaxPages; pageNumber++)
        {
            if (pageNumber > 0)
                await Task.Delay(TimeSpan.FromMilliseconds(1600), cancellationToken);
            var uri = new Uri(OfficialBase, $"localidades?max={PageSize}&inicio={start}");
            var json = await ProviderJsonTransport.GetAsync(client, uri, cancellationToken);
            GeoRefLocalityPage page;
            try
            {
                page = GeoRefLocalityParser.Parse(json);
            }
            catch (JsonException exception)
            {
                throw new ProviderFetchException(ProviderFetchFailure.SchemaMismatch,
                    "GeoRef locality catalog schema changed.", innerException: exception);
            }
            if (page.Start != start || page.Count > PageSize ||
                page.Count == 0 && start < page.Total)
                throw new ProviderFetchException(ProviderFetchFailure.IncompleteCatalog,
                    "GeoRef locality pagination is inconsistent.");
            pages.Add(page);
            start += page.Count;
            if (start == page.Total)
            {
                try
                {
                    return GeoRefSnapshotBuilder.Build(pages);
                }
                catch (InvalidDataException exception)
                {
                    throw new ProviderFetchException(ProviderFetchFailure.IncompleteCatalog,
                        "GeoRef locality catalog is incomplete.", innerException: exception);
                }
            }
        }

        throw new ProviderFetchException(ProviderFetchFailure.IncompleteCatalog,
            "GeoRef locality catalog exceeded the page limit.");
    }
}
