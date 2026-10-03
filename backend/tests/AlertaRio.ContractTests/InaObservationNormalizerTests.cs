using System.Text.Json;
using AlertaRio.Infrastructure.Providers;
using Xunit;

namespace AlertaRio.ContractTests;

public sealed class InaObservationNormalizerTests
{
    private static readonly InaSeriesContext Series = new(9, 3, "m", true, true);
    private static readonly DateTimeOffset IngestedAt =
        new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Both_catalog_wrappers_produce_the_same_unapproved_candidate()
    {
        const string row = """
            {"id":11,"series_id":9,"unit_id":null,"valor":7.48,
             "timestart":"2026-10-01T03:00:00-03:00",
             "timeend":"2026-10-01T03:15:00-03:00",
             "timeupdate":"2026-10-01T10:00:00Z"}
            """;
        var flat = InaObservationNormalizer.Normalize($"[{row}]", Series, IngestedAt);
        var wrapped = InaObservationNormalizer.Normalize(
            $$"""{"rows":[{{row}}],"total":1}""", Series, IngestedAt);

        var first = Assert.Single(flat);
        var second = Assert.Single(wrapped);
        Assert.Equal(InaObservationStatus.Candidate, first.Status);
        Assert.Equal(first.Candidate, second.Candidate);
        Assert.Equal("m", first.Candidate!.Unit);
        Assert.Equal(7.48m, first.Candidate.Value);
        Assert.Equal("7.48", first.Candidate.OriginalValue);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 6, 0, 0, TimeSpan.Zero),
            first.Candidate.ObservedStartAt);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 6, 15, 0, TimeSpan.Zero),
            first.Candidate.ObservedEndAt);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero),
            first.Candidate.SourceUpdatedAt);
        Assert.Equal(IngestedAt, first.Candidate.IngestedAt);
    }

    [Fact]
    public void Missing_values_are_distinct_from_conflicts_and_ambiguous_times()
    {
        const string json = """
            [
              {"id":1,"series_id":9,"unit_id":null,"valor":null,"timestart":"2026-10-01T03:00:00Z"},
              {"id":2,"series_id":9,"unit_id":null,"valor":"null","timestart":"2026-10-01T03:00:00Z"},
              {"id":3,"series_id":9,"unit_id":4,"valor":0,"timestart":"2026-10-01T03:00:00Z"},
              {"id":4,"series_id":9,"unit_id":3,"valor":1,"timestart":"2026-10-01T03:00:00"},
              {"id":5,"series_id":9,"unit_id":3,"valor":1,"timestart":"2026-10-03T03:00:00Z"},
              {"id":6,"series_id":8,"unit_id":3,"valor":1,"timestart":"2026-10-01T03:00:00Z"},
              {"id":7,"series_id":9,"unit_id":3,"valor":"7.48","timestart":"2026-10-01T03:00:00Z"},
              {"id":8,"series_id":9,"unit_id":3,"valor":0,"timestart":"2026-10-01T03:00:00Z"},
              {"id":8,"series_id":9,"unit_id":3,"valor":1,"timestart":"2026-10-01T03:00:00Z"}
            ]
            """;
        var results = InaObservationNormalizer.Normalize(json, Series, IngestedAt);

        Assert.Equal(new[]
        {
            "missingValue", "missingValue", "unitConflict", "ambiguousObservedTime",
            "futureObservation", "seriesMismatch", "invalidValue", null,
            "duplicateObservationId"
        }, results.Select(result => result.Reason));
        Assert.Equal(InaObservationStatus.Candidate, results[7].Status);
        Assert.Equal(0m, results[7].Candidate!.Value);
        Assert.All(results.Take(2), result =>
        {
            Assert.Equal(InaObservationStatus.Missing, result.Status);
            Assert.NotNull(result.Missing);
            Assert.Equal(new DateTimeOffset(2026, 10, 1, 3, 0, 0, TimeSpan.Zero),
                result.Missing.ObservedStartAt);
        });
        Assert.All(results.Where(result => result.Status == InaObservationStatus.Quarantined),
            result => Assert.Equal(64, result.RawPayloadHash?.Length));
        Assert.All(results.Where((_, index) => index != 7),
            result => Assert.Null(result.Candidate));
    }

    [Fact]
    public void Unsupported_series_and_unknown_wrappers_never_produce_candidates()
    {
        const string row = """
            [{"id":11,"series_id":9,"unit_id":3,"valor":7.48,
              "timestart":"2026-10-01T03:00:00Z"}]
            """;
        var simulated = InaObservationNormalizer.Normalize(row,
            Series with { IsObserved = false }, IngestedAt);
        Assert.Equal("unsupportedSeriesKind", Assert.Single(simulated).Reason);
        var unknownUnit = InaObservationNormalizer.Normalize(row,
            Series with { UnitId = null }, IngestedAt);
        Assert.Equal("seriesUnitUnverified", Assert.Single(unknownUnit).Reason);
        Assert.Throws<JsonException>(() => InaObservationNormalizer.Normalize(
            "{\"items\":[]}", Series, IngestedAt));
    }

    [Fact]
    public void Large_INA_observation_ids_remain_valid_and_detect_duplicates()
    {
        const string json = """
            [
              {"id":29286744105,"series_id":9,"unit_id":null,"valor":1,
               "timestart":"2026-10-01T03:00:00Z"},
              {"id":29286744105,"series_id":9,"unit_id":null,"valor":2,
               "timestart":"2026-10-01T04:00:00Z"}
            ]
            """;

        var results = InaObservationNormalizer.Normalize(json, Series, IngestedAt);

        Assert.Equal(29286744105L, results[0].Candidate!.ExternalObservationId);
        Assert.Equal("duplicateObservationId", results[1].Reason);
    }
}
