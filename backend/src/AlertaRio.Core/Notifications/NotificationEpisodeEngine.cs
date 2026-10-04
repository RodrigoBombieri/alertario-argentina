using AlertaRio.Core.Trends;

namespace AlertaRio.Core.Notifications;

public sealed record NotificationRule(
    Guid Id, Guid SeriesId, CalculatedCondition Trigger,
    TimeSpan MaximumObservationAge, TimeSpan DeliveryTtl, bool Enabled);

public sealed record NotificationSignal(
    Guid SeriesId, DateTimeOffset ObservedAt, DataStatus DataStatus,
    CalculatedCondition Condition, bool SourceApproved,
    bool IsLatest, bool IsBackfill);

public sealed record NotificationEpisode(
    Guid RuleId, CalculatedCondition Trigger, DateTimeOffset OpenedAt);

public enum NotificationTransition { None, Open, Close }

public sealed record NotificationDecision(
    NotificationTransition Transition, DateTimeOffset? ExpiresAt = null);

// Decisions are deterministic; persistence and delivery must use a durable outbox.
public static class NotificationEpisodeEngine
{
    public static NotificationDecision Evaluate(
        NotificationRule rule, NotificationEpisode? active,
        NotificationSignal signal, DateTimeOffset now)
    {
        if (rule.Id == Guid.Empty || rule.SeriesId == Guid.Empty ||
            rule.MaximumObservationAge <= TimeSpan.Zero ||
            rule.DeliveryTtl <= TimeSpan.Zero ||
            rule.Trigger is not (CalculatedCondition.FollowUp or
                CalculatedCondition.AboveAlertThreshold or
                CalculatedCondition.AboveEvacuationThreshold))
            throw new ArgumentException("Invalid notification rule.", nameof(rule));
        if (active is not null &&
            (active.RuleId != rule.Id || active.Trigger != rule.Trigger))
            throw new ArgumentException("Episode does not belong to the rule.", nameof(active));
        if (!rule.Enabled)
            return new NotificationDecision(active is null
                ? NotificationTransition.None : NotificationTransition.Close);
        if (signal.SeriesId != rule.SeriesId || !signal.SourceApproved ||
            !signal.IsLatest || signal.IsBackfill ||
            signal.DataStatus != DataStatus.Current ||
            signal.ObservedAt > now ||
            now - signal.ObservedAt > rule.MaximumObservationAge)
            return new NotificationDecision(NotificationTransition.None);
        if (signal.Condition != rule.Trigger)
            return new NotificationDecision(active is null
                ? NotificationTransition.None : NotificationTransition.Close);
        if (active is not null)
            return new NotificationDecision(NotificationTransition.None);
        var expiresAt = signal.ObservedAt + rule.MaximumObservationAge;
        var ttlExpiresAt = now + rule.DeliveryTtl;
        if (ttlExpiresAt < expiresAt) expiresAt = ttlExpiresAt;
        return expiresAt <= now
            ? new NotificationDecision(NotificationTransition.None)
            : new NotificationDecision(NotificationTransition.Open, expiresAt);
    }
}
