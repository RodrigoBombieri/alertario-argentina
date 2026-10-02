using System.Text.Json;
using AlertaRio.Infrastructure.Providers;
using Xunit;

namespace AlertaRio.ContractTests;

public sealed class InaSeriesCandidateParserTests
{
    [Fact]
    public void Series_keep_network_unit_procedure_and_raw_catalog_dates()
    {
        const string json = """
            {"rows":[
              {"id":31,"tipo":"puntual",
               "estacion":{"id":21,"public":true,"red":{"id":4,"public":true}},
               "var":{"id":2,"var":"H","nombre":"Altura","timeSupport":{"years":0,"months":0,"days":0,
                       "hours":0,"minutes":0,"seconds":0,"milliseconds":0}},
               "procedimiento":{"id":5,"nombre":"medición directa"},
               "unidades":{"id":3,"abrev":"m"},
               "date_range":{"timestart":"2026-09-01T00:00:00",
                             "timeend":"2026-10-01T00:00:00"}},
              {"id":32,"tipo":"puntual",
               "estacion":{"id":21,"public":true,"red":{"id":4,"public":true}},
               "var":{"var":"H","timeSupport":{"years":0,"months":0,"days":1,
                       "hours":0,"minutes":0,"seconds":0,"milliseconds":0}},
               "procedimiento":{"nombre":"promedio diario"},
               "unidades":{"id":3,"abrev":"m"},"date_range":null},
              {"id":33,"estacion":{"public":false,"red":{"public":true}}}
            ]}
            """;

        var candidates = InaSeriesCandidateParser.Parse(json);

        Assert.Equal(2, candidates.Count);
        var instantaneous = candidates[0];
        Assert.Equal(31, instantaneous.ExternalSeriesId);
        Assert.Equal(21, instantaneous.ExternalStationId);
        Assert.Equal(4, instantaneous.NetworkId);
        Assert.Equal("H", instantaneous.VariableCode);
        Assert.Equal(2, instantaneous.VariableId);
        Assert.Equal("Altura", instantaneous.VariableName);
        Assert.Equal(5, instantaneous.ProcedureId);
        Assert.Equal("medición directa", instantaneous.ProcedureName);
        Assert.Equal(3, instantaneous.UnitId);
        Assert.Equal("m", instantaneous.Unit);
        Assert.True(instantaneous.IsInstantaneous);
        Assert.Equal("2026-10-01T00:00:00", instantaneous.CatalogEndRaw);
        Assert.False(candidates[1].IsInstantaneous);
        Assert.Null(candidates[1].CatalogEndRaw);
    }

    [Fact]
    public void Unknown_unit_or_invalid_time_support_rejects_the_page()
    {
        const string missingUnit = """
            {"rows":[{"id":31,"tipo":"puntual",
              "estacion":{"id":21,"public":true,"red":{"id":4,"public":true}},
              "var":{"var":"H","timeSupport":{"years":0,"months":0,"days":0,
                    "hours":0,"minutes":0,"seconds":0,"milliseconds":0}},
              "procedimiento":{"nombre":"medición directa"},
              "unidades":{"abrev":"m"}}]}
            """;
        const string negativeSupport = """
            {"rows":[{"id":31,"tipo":"puntual",
              "estacion":{"id":21,"public":true,"red":{"id":4,"public":true}},
              "var":{"var":"H","timeSupport":{"years":0,"months":0,"days":-1,
                    "hours":0,"minutes":0,"seconds":0,"milliseconds":0}},
              "procedimiento":{"nombre":"medición directa"},
              "unidades":{"id":3,"abrev":"m"}}]}
            """;

        Assert.Throws<JsonException>(() => InaSeriesCandidateParser.Parse(missingUnit));
        Assert.Throws<JsonException>(() => InaSeriesCandidateParser.Parse(negativeSupport));
    }
}
