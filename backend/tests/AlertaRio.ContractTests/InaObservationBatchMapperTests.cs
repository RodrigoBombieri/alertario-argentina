using AlertaRio.Infrastructure.Ingestion;
using AlertaRio.Infrastructure.Providers;
using Xunit;

namespace AlertaRio.ContractTests;

public sealed class InaObservationBatchMapperTests
{
    private static readonly DateTimeOffset IngestedAt =
        new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid InternalSeriesId =
        Guid.Parse("00000000-0000-4000-8000-000000000501");
    private static readonly InaTimeSupport Instant = new(0, 0, 0, 0, 0, 0, 0);
    private static readonly InaSeriesCandidate Series = new(
        31, 21, 4, 2, "H", "Altura", 5, "medición directa", 3, "m",
        Instant, null, null);
    private static readonly InaSeriesApproval Approval = new(
        31, 21, 4, "H", 5, "medición directa", 3, "m", Instant,
        InaSeriesDataKind.Observed, "synthetic-v1", "synthetic-rights",
        "synthetic-hydrology");

    [Fact]
    public void Stable_source_payload_has_stable_hash_across_ingestion_retries()
    {
        const string json = """
            [{"id":11,"series_id":31,"unit_id":3,"valor":7.48,
              "timestart":"2026-10-03T06:00:00Z",
              "timeupdate":"2026-10-03T07:00:00Z"}]
            """;
        var selected = InaSeriesApprovalGate.Select(Series, Approval);
        var first = Map(selected, json, IngestedAt);
        var retry = Map(selected, json, IngestedAt.AddMinutes(2));
        var record = Assert.Single(first.Records);

        Assert.Equal("ina", first.Provider);
        Assert.Equal("complete", first.Coverage);
        Assert.Equal(InternalSeriesId, record.SeriesId);
        Assert.Equal("11", record.SourceRecordId);
        Assert.Equal(record.ObservedStartAt, record.ObservedEndAt);
        Assert.Equal(64, record.PayloadHash.Length);
        Assert.Equal(record.PayloadHash, Assert.Single(retry.Records).PayloadHash);
    }

    [Fact]
    public void Source_correction_changes_hash_and_rejected_rows_mark_partial_coverage()
    {
        var selected = InaSeriesApprovalGate.Select(Series, Approval);
        const string original = """
            [{"id":11,"series_id":31,"unit_id":3,"valor":7.48,
              "timestart":"2026-10-03T06:00:00Z",
              "timeupdate":"2026-10-03T07:00:00Z"}]
            """;
        const string corrected = """
            [{"id":11,"series_id":31,"unit_id":3,"valor":7.50,
              "timestart":"2026-10-03T06:00:00Z",
              "timeupdate":"2026-10-03T08:00:00Z"},
             {"id":12,"series_id":31,"unit_id":3,"valor":null,
              "timestart":"2026-10-03T07:00:00Z"}]
            """;
        var before = Map(selected, original, IngestedAt);
        var after = Map(selected, corrected, IngestedAt);

        Assert.NotEqual(Assert.Single(before.Records).PayloadHash,
            after.Records[0].PayloadHash);
        Assert.Equal(2, after.Records.Count);
        Assert.Null(after.Records[1].Value);
        Assert.Equal("12", after.Records[1].SourceRecordId);
        Assert.Equal("partial", after.Coverage);
    }

    [Fact]
    public void Quarantined_input_keeps_hash_and_reason_for_durable_record()
    {
        var selected = InaSeriesApprovalGate.Select(Series, Approval);
        const string invalid = """
            [{"id":11,"series_id":31,"unit_id":4,"valor":7.48,
              "timestart":"2026-10-03T06:00:00Z"}]
            """;
        var batch = Map(selected, invalid, IngestedAt);
        Assert.Empty(batch.Records);
        var rejected = Assert.Single(batch.RejectedRecords!);
        Assert.Equal("11", rejected.SourceRecordId);
        Assert.Equal("normalizer:unitConflict", rejected.Reason);
        Assert.Equal(64, rejected.PayloadHash.Length);
        Assert.Equal("partial", batch.Coverage);
    }

    [Fact]
    public void Candidate_from_another_series_cannot_enter_the_batch()
    {
        var selected = InaSeriesApprovalGate.Select(Series, Approval);
        var candidate = new InaObservationCandidate(11, 99, 7m, "7", "m",
            IngestedAt.AddHours(-1), null, null, IngestedAt);
        Assert.Throws<InvalidDataException>(() => InaObservationBatchMapper.Map(
            selected, InternalSeriesId,
            [new InaObservationResult(11, InaObservationStatus.Candidate, null, candidate)],
            InternalSeriesId.ToString("D"), "worker", "cursor", IngestedAt));
    }

    private static IngestionBatch Map(
        ApprovedInaSeries selected, string json, DateTimeOffset ingestedAt) =>
        InaObservationBatchMapper.Map(selected, InternalSeriesId,
            InaObservationNormalizer.Normalize(json, selected.Context, ingestedAt),
            InternalSeriesId.ToString("D"), "worker", "cursor", ingestedAt);
}
