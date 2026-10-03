namespace AlertaRio.Infrastructure.Ingestion;

public sealed record MeasurementIdentity(
    Guid SeriesId, DateTimeOffset ObservedStartAt, DateTimeOffset ObservedEndAt);

public sealed record IncomingMeasurement(
    MeasurementIdentity Identity, decimal? Value, DateTimeOffset? SourceUpdatedAt,
    DateTimeOffset IngestedAt, string PayloadHash);

public sealed record StoredMeasurementVersion(
    MeasurementIdentity Identity, decimal? Value, DateTimeOffset? SourceUpdatedAt,
    string PayloadHash, int Revision);

public enum MeasurementAction
{
    Insert,
    Unchanged,
    Revise,
    Quarantine
}

public sealed record MeasurementDecision(
    MeasurementAction Action, int? NextRevision, string? Reason);

// Pure decision only. The F4 writer must apply it with latest and checkpoint in one transaction.
public static class MeasurementReconciler
{
    public static MeasurementDecision Decide(
        StoredMeasurementVersion? current, IncomingMeasurement incoming)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(incoming.Identity);
        if (incoming.Identity.SeriesId == Guid.Empty ||
            incoming.Identity.ObservedEndAt < incoming.Identity.ObservedStartAt ||
            incoming.Identity.ObservedStartAt > incoming.IngestedAt ||
            incoming.PayloadHash.Length != 64 ||
            incoming.PayloadHash.Any(character => character is not (>= '0' and <= '9' or
                >= 'a' and <= 'f')))
            throw new ArgumentException("Incoming measurement has invalid identity or hash.",
                nameof(incoming));

        if (current is null)
            return new MeasurementDecision(MeasurementAction.Insert, 1, null);
        if (current.Identity != incoming.Identity || current.Revision < 1)
            return new MeasurementDecision(MeasurementAction.Quarantine, null,
                "identityOrRevisionMismatch");
        if (current.PayloadHash == incoming.PayloadHash)
            return current.Value == incoming.Value
                ? new MeasurementDecision(MeasurementAction.Unchanged, null, null)
                : new MeasurementDecision(MeasurementAction.Quarantine, null,
                    "hashValueConflict");
        if (incoming.SourceUpdatedAt is null ||
            current.SourceUpdatedAt is { } previous &&
            incoming.SourceUpdatedAt <= previous)
            return new MeasurementDecision(MeasurementAction.Quarantine, null,
                "unorderedCorrection");
        if (current.Revision == int.MaxValue)
            return new MeasurementDecision(MeasurementAction.Quarantine, null,
                "revisionOverflow");
        return new MeasurementDecision(MeasurementAction.Revise, current.Revision + 1, null);
    }
}
