using System.Text.Json;
using AlertaRio.Infrastructure.Providers;
using Xunit;

namespace AlertaRio.ContractTests;

public sealed class InaStationCandidateParserTests
{
    [Fact]
    public void Catalog_preserves_external_identity_and_longitude_latitude_order()
    {
        const string json = """
            {"estaciones":[
              {"id":21,"tabla":"red-ejemplo","id_externo":700,
               "nombre":"Estación de ejemplo","rio":"Río de ejemplo","public":true,
               "red":{"id":4,"public":true},
               "geom":{"type":"Point","coordinates":[-58.25,-31.25]}},
              {"id":22,"nombre":"Otra","public":true,"red":{"public":false}}
            ]}
            """;

        var candidate = Assert.Single(InaStationCandidateParser.Parse(json));

        Assert.Equal(21, candidate.ExternalId);
        Assert.Equal(4, candidate.NetworkId);
        Assert.Equal("red-ejemplo", candidate.NetworkCode);
        Assert.Equal("700", candidate.OwnerStationId);
        Assert.Equal(-58.25, candidate.Longitude);
        Assert.Equal(-31.25, candidate.Latitude);
    }

    [Fact]
    public void Invalid_geometry_or_duplicate_network_identity_rejects_the_page()
    {
        const string badPoint = """
            {"estaciones":[
              {"id":21,"tabla":"red-ejemplo","nombre":"Estación de ejemplo",
               "rio":"Río de ejemplo","public":true,"red":{"id":4,"public":true},
               "geom":{"type":"Point","coordinates":[-181,-31.25]}}
            ]}
            """;
        const string duplicate = """
            {"estaciones":[
              {"id":21,"tabla":"red-ejemplo","nombre":"Estación de ejemplo",
               "rio":"Río de ejemplo","public":true,"red":{"id":4,"public":true},
               "geom":{"type":"Point","coordinates":[-58.25,-31.25]}},
              {"id":21,"tabla":"red-ejemplo","nombre":"Otra estación",
               "rio":"Río de ejemplo","public":true,"red":{"id":4,"public":true},
               "geom":{"type":"Point","coordinates":[-58.5,-31.5]}}
            ]}
            """;

        Assert.Throws<JsonException>(() => InaStationCandidateParser.Parse(badPoint));
        Assert.Throws<JsonException>(() => InaStationCandidateParser.Parse(duplicate));
    }
}
