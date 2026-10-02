using System.Text.Json;
using AlertaRio.Infrastructure.Providers;
using Xunit;

namespace AlertaRio.ContractTests;

public sealed class GeoRefLocalityParserTests
{
    [Fact]
    public void Homonyms_keep_their_own_text_ids_categories_and_coordinates()
    {
        const string json = """
            {
              "cantidad":2,"inicio":0,"total":2,
              "localidades":[
                {"id":"030015060","nombre":"Villa Ejemplo","categoria":"Localidad simple",
                 "provincia":{"id":"30","nombre":"Provincia de ejemplo"},
                 "centroide":{"lat":-31.25,"lon":-58.25}},
                {"id":"03001506005","nombre":"Villa Ejemplo","categoria":"Entidad",
                 "provincia":{"id":"30","nombre":"Provincia de ejemplo"},"centroide":null}
              ]
            }
            """;

        var page = GeoRefLocalityParser.Parse(json);

        Assert.Equal(0, page.Start);
        Assert.Equal(2, page.Total);
        Assert.Equal(2, page.Count);
        Assert.Equal("030015060", page.Candidates[0].ExternalId);
        Assert.Equal("03001506005", page.Candidates[1].ExternalId);
        Assert.Equal("Localidad simple", page.Candidates[0].Category);
        Assert.Equal("Entidad", page.Candidates[1].Category);
        Assert.Equal(-31.25, page.Candidates[0].Latitude);
        Assert.Null(page.Candidates[1].Latitude);
        Assert.All(page.Candidates, candidate => Assert.Equal("Villa Ejemplo", candidate.Name));
    }

    [Fact]
    public void Invalid_page_or_coordinates_cannot_replace_a_catalog_snapshot()
    {
        const string badCount = """
            {"cantidad":2,"inicio":0,"total":2,"localidades":[]}
            """;
        const string badCentroid = """
            {"cantidad":1,"inicio":0,"total":1,"localidades":[
              {"id":"01","nombre":"Ejemplo","categoria":"Localidad simple",
               "provincia":{"id":"01","nombre":"Provincia"},
               "centroide":{"lat":91,"lon":-58}}
            ]}
            """;

        Assert.Throws<JsonException>(() => GeoRefLocalityParser.Parse(badCount));
        Assert.Throws<JsonException>(() => GeoRefLocalityParser.Parse(badCentroid));
    }
}
