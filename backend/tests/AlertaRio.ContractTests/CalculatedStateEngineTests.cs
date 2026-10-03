using AlertaRio.Core.Trends;
using Xunit;

namespace AlertaRio.ContractTests;

public sealed class CalculatedStateEngineTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid SeriesId =
        Guid.Parse("00000000-0000-4000-8000-000000000401");
    private static readonly CurrentLevel Level =
        new(SeriesId, Now, 12.50m, "m", "synthetic-datum", 1, true);
    private static readonly TrendPolicy Policy =
        new(TimeSpan.FromHours(1), TimeSpan.FromMinutes(30), 0.02m, "synthetic-v1");

    [Fact]
    public void Threshold_crossing_never_creates_an_official_notice_or_order()
    {
        var state = Evaluate(FreshTrend(), Level,
            [Threshold(CalculatedCondition.AboveAlertThreshold, 11m),
             Threshold(CalculatedCondition.AboveEvacuationThreshold, 12m)],
            [], NoticeCoverage.Complete);

        Assert.Equal(CalculatedCondition.AboveEvacuationThreshold, state.Condition);
        Assert.Equal("evacuation-reference", state.MatchedThresholdId);
        Assert.Empty(state.ActiveOfficialNotices);
        Assert.True(state.CanSayNoCurrentOfficialNotices);
    }

    [Fact]
    public void Stale_measurement_keeps_a_verified_official_notice()
    {
        var oldLevel = Level with { ObservedAt = Now.AddHours(-3) };
        var trend = TrendEngine.Calculate(
            [Sample(-9, 7.2m), Sample(-3, 7.4m)], Policy, Now);
        var notice = Notice("notice-1", 1);
        var state = Evaluate(trend, oldLevel, [], [notice], NoticeCoverage.Unavailable);

        Assert.Equal(DataStatus.Stale, state.DataStatus);
        Assert.Equal(CalculatedCondition.Unavailable, state.Condition);
        Assert.Single(state.ActiveOfficialNotices);
        Assert.False(state.CanSayNoCurrentOfficialNotices);
    }

    [Fact]
    public void Failed_notice_query_does_not_claim_there_are_no_notices()
    {
        var state = Evaluate(FreshTrend(), Level, [], [], NoticeCoverage.Unavailable);
        Assert.False(state.CanSayNoCurrentOfficialNotices);
    }

    [Fact]
    public void Unverified_notice_prevents_an_absence_claim_even_with_complete_feed()
    {
        var state = Evaluate(FreshTrend(), Level, [],
            [Notice("pending", 1) with { Verified = false }],
            NoticeCoverage.Complete);
        Assert.Empty(state.ActiveOfficialNotices);
        Assert.Equal(NoticeCoverage.Partial, state.NoticeCoverage);
        Assert.False(state.CanSayNoCurrentOfficialNotices);
    }

    [Fact]
    public void Cancelled_or_expired_notice_is_not_active_even_when_arrival_is_out_of_order()
    {
        var cancelled = Notice("notice-1", 2) with { Cancelled = true };
        var expired = Notice("notice-2", 1) with
        {
            EffectiveUntil = Now.AddSeconds(-1)
        };
        var state = Evaluate(FreshTrend(), Level, [],
            [cancelled, Notice("notice-1", 1), expired], NoticeCoverage.Complete);
        Assert.Empty(state.ActiveOfficialNotices);
        Assert.True(state.CanSayNoCurrentOfficialNotices);
    }

    [Fact]
    public void Follow_up_requires_explicit_policy_and_valid_six_hour_window()
    {
        var trend = FreshTrend();
        var state = CalculatedStateEngine.Evaluate(trend, Level, [],
            new FollowUpRule(6, 0.20m, true), [], NoticeCoverage.Complete, Now);
        Assert.Equal(CalculatedCondition.FollowUp, state.Condition);
        Assert.Equal(6, state.FollowUpWindowHours);

        var unapproved = CalculatedStateEngine.Evaluate(trend, Level, [],
            new FollowUpRule(6, 0.20m, false), [], NoticeCoverage.Complete, Now);
        Assert.Equal(CalculatedCondition.NoNotableChange, unapproved.Condition);
        var insufficient = Evaluate(TrendEngine.Calculate([Sample(0, 12.5m)], Policy, Now),
            Level, [], [], NoticeCoverage.Complete);
        Assert.Equal(CalculatedCondition.Unavailable, insufficient.Condition);
    }

    [Fact]
    public void Unapproved_expired_or_incompatible_threshold_cannot_be_compared()
    {
        var candidates = new[]
        {
            Threshold(CalculatedCondition.AboveAlertThreshold, 11m) with { Approved = false },
            Threshold(CalculatedCondition.AboveAlertThreshold, 11m) with
                { ValidUntil = Now.AddMinutes(-1) },
            Threshold(CalculatedCondition.AboveAlertThreshold, 11m) with { Datum = "other" },
            Threshold(CalculatedCondition.AboveAlertThreshold, 11m) with { Epoch = 2 }
        };
        var state = Evaluate(FreshTrend(), Level, candidates, [], NoticeCoverage.Complete);
        Assert.Equal(CalculatedCondition.NoNotableChange, state.Condition);
        Assert.Null(state.MatchedThresholdId);
    }

    [Fact]
    public void Level_must_match_the_latest_trend_sample_before_any_comparison()
    {
        var mismatched = Evaluate(FreshTrend(), Level with { Value = 13m },
            [Threshold(CalculatedCondition.AboveAlertThreshold, 11m)],
            [], NoticeCoverage.Complete);
        Assert.Equal(CalculatedCondition.Unavailable, mismatched.Condition);
    }

    [Fact]
    public void Suspect_reading_blocks_calculation_without_hiding_official_notice()
    {
        var trend = TrendEngine.Calculate(
            [Sample(-6, 12.2m), Sample(0, 12.5m),
             Sample(0.1, 13m) with { Quality = TrendSampleQuality.Suspect }],
            Policy, Now.AddMinutes(10));
        var state = CalculatedStateEngine.Evaluate(trend, Level,
            [Threshold(CalculatedCondition.AboveAlertThreshold, 11m)],
            new FollowUpRule(6, 0.20m, true), [Notice("notice-1", 1)],
            NoticeCoverage.Partial, Now.AddMinutes(10));
        Assert.Equal(DataStatus.QualityReview, state.DataStatus);
        Assert.Equal(CalculatedCondition.Unavailable, state.Condition);
        Assert.Single(state.ActiveOfficialNotices);
    }

    private static CalculatedState Evaluate(
        TrendSnapshot trend, CurrentLevel? level,
        IReadOnlyList<OfficialThreshold> thresholds,
        IReadOnlyList<OfficialNotice> notices, NoticeCoverage coverage) =>
        CalculatedStateEngine.Evaluate(trend, level, thresholds, null,
            notices, coverage, Now);

    private static TrendSnapshot FreshTrend() => TrendEngine.Calculate(
        [Sample(-6, 12.20m), Sample(0, 12.50m)], Policy, Now);

    private static TrendSample Sample(double hours, decimal value) =>
        new(Now.AddHours(hours), value, "m", "synthetic-datum", 1,
            "synthetic-direct", TrendSampleQuality.Accepted, 1);

    private static OfficialThreshold Threshold(
        CalculatedCondition condition, decimal value) =>
        new(condition == CalculatedCondition.AboveEvacuationThreshold
                ? "evacuation-reference" : "alert-reference",
            SeriesId, value, "m", "synthetic-datum", 1, condition,
            Now.AddDays(-1), null, true);

    private static OfficialNotice Notice(string id, int revision) =>
        new(id, "Synthetic authority", OfficialNoticeKind.Advisory,
            revision, Now.AddHours(-1), Now.AddHours(1), false, true, true);
}
