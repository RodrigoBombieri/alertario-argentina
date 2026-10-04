using AlertaRio.Core.Notifications;
using AlertaRio.Core.Trends;
using Xunit;

namespace AlertaRio.ContractTests;

public sealed class NotificationEpisodeEngineTests
{
    private readonly Guid _seriesId = Guid.NewGuid();
    private readonly Guid _ruleId = Guid.NewGuid();
    private readonly DateTimeOffset _now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Opens_once_and_closes_only_after_a_current_clear_signal()
    {
        var rule = Rule();
        var signal = Signal();
        var opened = NotificationEpisodeEngine.Evaluate(rule, null, signal, _now);
        Assert.Equal(NotificationTransition.Open, opened.Transition);
        Assert.Equal(_now.AddMinutes(20), opened.ExpiresAt);
        var episode = new NotificationEpisode(rule.Id, rule.Trigger, _now);
        Assert.Equal(NotificationTransition.None,
            NotificationEpisodeEngine.Evaluate(rule, episode, signal, _now).Transition);
        Assert.Equal(NotificationTransition.None,
            NotificationEpisodeEngine.Evaluate(rule, episode,
                signal with { DataStatus = DataStatus.Stale }, _now).Transition);
        Assert.Equal(NotificationTransition.Close,
            NotificationEpisodeEngine.Evaluate(rule, episode,
                signal with { Condition = CalculatedCondition.NoNotableChange }, _now).Transition);
    }

    [Fact]
    public void Backfill_replay_unapproved_and_old_data_never_open_an_episode()
    {
        var rule = Rule();
        var signal = Signal();
        Assert.Equal(NotificationTransition.None,
            NotificationEpisodeEngine.Evaluate(rule, null,
                signal with { IsBackfill = true }, _now).Transition);
        Assert.Equal(NotificationTransition.None,
            NotificationEpisodeEngine.Evaluate(rule, null,
                signal with { IsLatest = false }, _now).Transition);
        Assert.Equal(NotificationTransition.None,
            NotificationEpisodeEngine.Evaluate(rule, null,
                signal with { SourceApproved = false }, _now).Transition);
        Assert.Equal(NotificationTransition.None,
            NotificationEpisodeEngine.Evaluate(rule, null,
                signal with { ObservedAt = _now.AddHours(-2) }, _now).Transition);
        Assert.Equal(NotificationTransition.None,
            NotificationEpisodeEngine.Evaluate(rule, null,
                signal with { DataStatus = DataStatus.QualityReview }, _now).Transition);
    }

    [Fact]
    public void Disabling_a_rule_closes_its_active_episode_without_a_new_push()
    {
        var rule = Rule() with { Enabled = false };
        var active = new NotificationEpisode(rule.Id, rule.Trigger, _now.AddMinutes(-10));
        Assert.Equal(NotificationTransition.Close,
            NotificationEpisodeEngine.Evaluate(rule, active, Signal(), _now).Transition);
    }

    private NotificationRule Rule() => new(_ruleId, _seriesId,
        CalculatedCondition.AboveAlertThreshold,
        TimeSpan.FromHours(1), TimeSpan.FromMinutes(20), true);

    private NotificationSignal Signal() => new(_seriesId, _now.AddMinutes(-5),
        DataStatus.Current, CalculatedCondition.AboveAlertThreshold,
        true, true, false);
}
