using AlertaRio.Infrastructure.Providers;
using Xunit;

namespace AlertaRio.ContractTests;

public sealed class GeoRefLocalitySearchTests
{
    private static readonly GeoRefCatalogSnapshot Snapshot = new(5,
    [
        new("001", "San José", "Localidad simple", "30", "Entre Ríos", -32.2, -58.1),
        new("002", "San Jose", "Entidad", "30", "Entre Ríos", -32.3, -58.2),
        new("003", "San José", "Localidad simple", "54", "Misiones", -27.8, -55.8),
        new("004", "Cañada de Gómez", "Localidad simple", "82", "Santa Fe", -32.8, -61.4),
        new("005", "Canada", "Localidad simple", "06", "Buenos Aires", -34.8, -58.4)
    ]);

    [Fact]
    public void Accent_insensitive_search_preserves_homonyms_and_province_identity()
    {
        var search = new GeoRefLocalitySearch(Snapshot);

        var matches = search.Search("  SAN, jose!  ");

        Assert.Equal(new[] { "002", "001", "003" },
            matches.Select(locality => locality.ExternalId));
        Assert.Equal(new[] { "Entidad", "Localidad simple", "Localidad simple" },
            matches.Select(locality => locality.Category));
        Assert.Equal(new[] { "30", "30", "54" },
            matches.Select(locality => locality.ProvinceId));
        Assert.Equal(new[] { "002", "001" },
            search.Search("san jose", provinceId: "30")
                .Select(locality => locality.ExternalId));
    }

    [Fact]
    public void Enye_is_not_conflated_with_plain_n_and_results_are_bounded()
    {
        var search = new GeoRefLocalitySearch(Snapshot);

        Assert.Equal("004", Assert.Single(search.Search("cañada")).ExternalId);
        Assert.Equal("005", Assert.Single(search.Search("canada")).ExternalId);
        Assert.Equal(2, search.Search("san jose", limit: 2).Count);
        Assert.Empty(search.Search("s"));
    }

    [Fact]
    public void Search_rejects_incomplete_snapshots_and_unbounded_requests()
    {
        Assert.Throws<ArgumentException>(() => new GeoRefLocalitySearch(
            Snapshot with { Total = 6 }));
        var search = new GeoRefLocalitySearch(Snapshot);
        Assert.Throws<ArgumentOutOfRangeException>(() => search.Search("san", limit: 51));
        Assert.Throws<ArgumentOutOfRangeException>(() => search.Search(new string('a', 81)));
    }
}
