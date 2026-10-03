using AlertaRio.Infrastructure.Ingestion;
using Xunit;

namespace AlertaRio.ContractTests;

public sealed class MeasurementReconcilerTests
{
    private static readonly MeasurementIdentity Identity = new(
        Guid.Parse("00000000-0000-4000-8000-000000000003"),
        new DateTimeOffset(2026, 10, 1, 3, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 10, 1, 3, 0, 0, TimeSpan.Zero));
    private static readonly DateTimeOffset SourceUpdatedAt =
        new(2026, 10, 1, 13, 0, 0, TimeSpan.Zero);
    private static readonly IncomingMeasurement Incoming = new(
        Identity, 0, SourceUpdatedAt,
        new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero), new string('a', 64));

    [Fact]
    public void First_import_is_insert_and_identical_replay_does_not_rejuvenate_it()
    {
        var first = MeasurementReconciler.Decide(null, Incoming);
        var stored = new StoredMeasurementVersion(
            Identity, 0, SourceUpdatedAt, Incoming.PayloadHash, 1);
        var replay = MeasurementReconciler.Decide(stored,
            Incoming with { IngestedAt = Incoming.IngestedAt.AddDays(1) });

        Assert.Equal(MeasurementAction.Insert, first.Action);
        Assert.Equal(1, first.NextRevision);
        Assert.Equal(MeasurementAction.Unchanged, replay.Action);
        Assert.Null(replay.NextRevision);
    }

    [Fact]
    public void Later_source_correction_revises_but_older_or_tied_conflicts_quarantine()
    {
        var stored = new StoredMeasurementVersion(
            Identity, 0, SourceUpdatedAt, Incoming.PayloadHash, 2);
        var changed = Incoming with
        {
            Value = 1,
            PayloadHash = new string('b', 64),
            SourceUpdatedAt = SourceUpdatedAt.AddHours(1)
        };

        Assert.Equal(new MeasurementDecision(MeasurementAction.Revise, 3, null),
            MeasurementReconciler.Decide(stored, changed));
        Assert.Equal("unorderedCorrection", MeasurementReconciler.Decide(
            stored, changed with { SourceUpdatedAt = SourceUpdatedAt }).Reason);
        Assert.Equal("unorderedCorrection", MeasurementReconciler.Decide(
            stored, changed with { SourceUpdatedAt = null }).Reason);
    }

    [Fact]
    public void Missing_and_zero_are_distinct_and_invalid_intervals_do_not_enter_a_batch()
    {
        var storedMissing = new StoredMeasurementVersion(
            Identity, null, SourceUpdatedAt, new string('c', 64), 1);
        Assert.Equal(MeasurementAction.Revise, MeasurementReconciler.Decide(
            storedMissing, Incoming with { SourceUpdatedAt = SourceUpdatedAt.AddHours(1) }).Action);
        Assert.Equal("hashValueConflict", MeasurementReconciler.Decide(
            storedMissing, Incoming with { PayloadHash = storedMissing.PayloadHash }).Reason);
        Assert.Throws<ArgumentException>(() => MeasurementReconciler.Decide(null,
            Incoming with
            {
                Identity = Identity with
                { ObservedEndAt = Identity.ObservedStartAt.AddSeconds(-1) }
            }));
    }
}
