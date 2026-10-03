namespace AlertaRio.Core.Trends;

public enum DataStatus { NoData, Current, Stale, Unverified, QualityReview }
public enum CalculatedCondition
{
    Unavailable,
    NoNotableChange,
    FollowUp,
    AboveAlertThreshold,
    AboveEvacuationThreshold
}
public enum NoticeCoverage { Complete, Partial, Unavailable }
public enum OfficialNoticeKind { Advisory, EvacuationOrder }

public sealed record CurrentLevel(
    Guid SeriesId, DateTimeOffset ObservedAt, decimal Value, string Unit,
    string Datum, int Epoch, bool ApprovedForCalculation);

public sealed record OfficialThreshold(
    string ReferenceId, Guid SeriesId, decimal Value, string Unit,
    string Datum, int Epoch, CalculatedCondition Condition,
    DateTimeOffset ValidFrom, DateTimeOffset? ValidUntil, bool Approved);

public sealed record FollowUpRule(int WindowHours, decimal MinimumRise, bool Approved);

public sealed record OfficialNotice(
    string Id, string Issuer, OfficialNoticeKind Kind, int Revision,
    DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveUntil,
    bool Cancelled, bool Verified, bool AppliesToStation);

public sealed record CalculatedState(
    DataStatus DataStatus, CalculatedCondition Condition,
    string? MatchedThresholdId, int? FollowUpWindowHours,
    NoticeCoverage NoticeCoverage, IReadOnlyList<OfficialNotice> ActiveOfficialNotices)
{
    public bool CanSayNoCurrentOfficialNotices =>
        NoticeCoverage == NoticeCoverage.Complete && ActiveOfficialNotices.Count == 0;
}

// A threshold crossing is a calculated comparison, never an official notice or order.
public static class CalculatedStateEngine
{
    public static CalculatedState Evaluate(
        TrendSnapshot trend, CurrentLevel? level,
        IReadOnlyList<OfficialThreshold> thresholds, FollowUpRule? followUp,
        IReadOnlyList<OfficialNotice> notices, NoticeCoverage noticeCoverage,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(trend);
        ArgumentNullException.ThrowIfNull(thresholds);
        ArgumentNullException.ThrowIfNull(notices);
        if (followUp is { MinimumRise: < 0 } ||
            followUp is { WindowHours: <= 0 })
            throw new ArgumentException("Invalid follow-up rule.", nameof(followUp));

        var effectiveNoticeCoverage = notices.Any(notice => notice.AppliesToStation &&
            (!notice.Verified || notice.Revision <= 0 ||
             string.IsNullOrWhiteSpace(notice.Id) ||
             string.IsNullOrWhiteSpace(notice.Issuer)))
            ? NoticeCoverage.Partial : noticeCoverage;

        var activeNotices = notices.Where(notice => notice.Verified &&
                notice.AppliesToStation && notice.Revision > 0 &&
                !string.IsNullOrWhiteSpace(notice.Id) &&
                !string.IsNullOrWhiteSpace(notice.Issuer))
            .GroupBy(notice => (notice.Issuer, notice.Id))
            .Select(group => group.OrderByDescending(notice => notice.Revision)
                .ThenByDescending(notice => notice.Cancelled).First())
            .Where(notice => !notice.Cancelled && notice.EffectiveFrom <= now &&
                (notice.EffectiveUntil is null || now < notice.EffectiveUntil))
            .OrderBy(notice => notice.Issuer, StringComparer.Ordinal)
            .ThenBy(notice => notice.Id, StringComparer.Ordinal)
            .ToArray();

        var dataStatus = trend.Freshness switch
        {
            TrendFreshness.NoData => DataStatus.NoData,
            TrendFreshness.Stale => DataStatus.Stale,
            TrendFreshness.Unknown => DataStatus.Unverified,
            _ when trend.HasRecentSuspect => DataStatus.QualityReview,
            _ => DataStatus.Current
        };
        var condition = CalculatedCondition.Unavailable;
        string? matchedThresholdId = null;
        int? followUpWindowHours = null;
        if (dataStatus == DataStatus.Current && level is not null &&
            level.ApprovedForCalculation && level.SeriesId != Guid.Empty &&
            trend.LatestObservedAt == level.ObservedAt &&
            trend.LatestValue == level.Value && trend.LatestUnit == level.Unit &&
            trend.LatestDatum == level.Datum && trend.LatestEpoch == level.Epoch)
        {
            var matchedThreshold = thresholds.Where(threshold =>
                    threshold.Approved && threshold.SeriesId == level.SeriesId &&
                    threshold.Unit == level.Unit && threshold.Datum == level.Datum &&
                    threshold.Epoch == level.Epoch &&
                    threshold.ValidFrom <= now &&
                    (threshold.ValidUntil is null || now < threshold.ValidUntil) &&
                    level.Value >= threshold.Value &&
                    threshold.Condition is CalculatedCondition.AboveAlertThreshold or
                        CalculatedCondition.AboveEvacuationThreshold)
                .OrderByDescending(threshold => threshold.Condition)
                .ThenByDescending(threshold => threshold.Value)
                .FirstOrDefault();
            if (matchedThreshold is not null)
            {
                condition = matchedThreshold.Condition;
                matchedThresholdId = matchedThreshold.ReferenceId;
            }
            else if (followUp is { Approved: true } && trend.CanTriggerCalculatedRule)
            {
                var window = trend.Windows.FirstOrDefault(result =>
                    result.WindowHours == followUp.WindowHours &&
                    result.Availability == TrendAvailability.Available &&
                    result.Delta >= followUp.MinimumRise);
                if (window is not null)
                {
                    condition = CalculatedCondition.FollowUp;
                    followUpWindowHours = window.WindowHours;
                }
            }
            if (condition == CalculatedCondition.Unavailable &&
                trend.CanTriggerCalculatedRule)
                condition = CalculatedCondition.NoNotableChange;
        }
        return new CalculatedState(dataStatus, condition, matchedThresholdId,
            followUpWindowHours, effectiveNoticeCoverage, activeNotices);
    }
}
