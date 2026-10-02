using AlertaRio.Infrastructure.Providers;
using Xunit;

namespace AlertaRio.ContractTests;

public sealed class GeoRefSnapshotBuilderTests
{
    private static readonly GeoRefLocalityCandidate First =
        new("001", "Villa Ejemplo", "Localidad simple", "01", "Provincia de ejemplo", -31.25, -58.25);
    private static readonly GeoRefLocalityCandidate Second =
        new("002", "Villa Ejemplo", "Entidad", "01", "Provincia de ejemplo", -31.3, -58.3);

    [Fact]
    public void Complete_pages_build_a_new_snapshot_without_merging_homonyms()
    {
        var pages = new[]
        {
            new GeoRefLocalityPage(1, 1, 2, [Second]),
            new GeoRefLocalityPage(0, 1, 2, [First])
        };

        var snapshot = GeoRefSnapshotBuilder.Build(pages);

        Assert.Equal(2, snapshot.Total);
        Assert.Equal(new[] { "001", "002" },
            snapshot.Localities.Select(locality => locality.ExternalId));
        Assert.All(snapshot.Localities,
            locality => Assert.Equal("Villa Ejemplo", locality.Name));
    }

    [Fact]
    public void Partial_empty_or_repeated_pages_cannot_replace_a_catalog()
    {
        var firstPage = new GeoRefLocalityPage(0, 1, 2, [First]);
        var duplicate = new GeoRefLocalityPage(1, 1, 2, [First]);
        var wrongTotal = new GeoRefLocalityPage(1, 1, 3, [Second]);

        Assert.Throws<InvalidDataException>(() => GeoRefSnapshotBuilder.Build([firstPage]));
        Assert.Throws<InvalidDataException>(() => GeoRefSnapshotBuilder.Build([firstPage, duplicate]));
        Assert.Throws<InvalidDataException>(() => GeoRefSnapshotBuilder.Build([firstPage, wrongTotal]));
        Assert.Throws<InvalidDataException>(() => GeoRefSnapshotBuilder.Build(
            [new GeoRefLocalityPage(0, 0, 0, [])]));
    }
}
