using AlertaRio.Core.Trends;
using Xunit;

namespace AlertaRio.ContractTests;

public sealed class TrendEngineTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly TrendPolicy Hourly =
        new(TimeSpan.FromHours(1), TimeSpan.FromMinutes(30), 0.02m, "synthetic-v1");

    [Theory]
    [InlineData(7.32, TrendDirection.Rising)]
    [InlineData(7.50, TrendDirection.Stable)]
    [InlineData(7.51, TrendDirection.Falling)]
    public void Six_hour_delta_uses_exact_observed_endpoints(
        double referenceValue, TrendDirection expected)
    {
        var snapshot = TrendEngine.Calculate(
            [Sample(-6, (decimal)referenceValue), Sample(0, 7.48m)], Hourly, Now);
        var sixHours = Window(snapshot, 6);

        Assert.Equal(7.48m - (decimal)referenceValue, sixHours.Delta);
        Assert.Equal(expected, sixHours.Direction);
        Assert.Equal(21600, sixHours.ActualDurationSeconds);
        Assert.Equal(6, snapshot.PrimaryWindowHours);
    }

    [Fact]
    public void Daily_series_does_not_invent_short_windows()
    {
        var policy = Hourly with
        {
            Cadence = TimeSpan.FromHours(24),
            AllowedLag = TimeSpan.FromHours(14)
        };
        var snapshot = TrendEngine.Calculate(
            [Sample(-24, 7.30m), Sample(0, 7.48m)], policy, Now);

        Assert.All(snapshot.Windows.Where(window => window.WindowHours < 24),
            window => Assert.Equal(TrendAvailability.InsufficientData,
                window.Availability));
        Assert.Equal(TrendDirection.Rising, Window(snapshot, 24).Direction);
        Assert.Equal(24, snapshot.PrimaryWindowHours);
    }

    [Fact]
    public void Tolerance_accepts_545_but_rejects_520_for_a_six_hour_window()
    {
        var inside = TrendEngine.Calculate(
            [Sample(-5.75, 7.32m), Sample(0, 7.48m)], Hourly, Now);
        var outside = TrendEngine.Calculate(
            [Sample(-5.333333333333333, 7.32m), Sample(0, 7.48m)], Hourly, Now);

        Assert.Equal(TrendAvailability.Available, Window(inside, 6).Availability);
        Assert.Equal(20700, Window(inside, 6).ActualDurationSeconds);
        Assert.Equal(TrendAvailability.InsufficientData, Window(outside, 6).Availability);
    }

    [Fact]
    public void Stale_or_unapproved_parameters_preserve_delta_without_current_trend()
    {
        var samples = new[] { Sample(-8, 7.32m), Sample(-2, 7.48m) };
        var stale = TrendEngine.Calculate(samples, Hourly, Now);
        var noCadence = TrendEngine.Calculate(samples,
            Hourly with { Cadence = null }, Now);
        var noEpsilon = TrendEngine.Calculate(
            [Sample(-6, 7.32m), Sample(0, 7.48m)],
            Hourly with { Epsilon = null }, Now);

        Assert.Equal(TrendFreshness.Stale, stale.Freshness);
        Assert.Equal(0.16m, Window(stale, 6).Delta);
        Assert.Equal(TrendAvailability.HistoricalOnly, Window(stale, 6).Availability);
        Assert.Null(stale.PrimaryWindowHours);
        Assert.Equal(TrendAvailability.FreshnessUnknown, Window(noCadence, 6).Availability);
        Assert.Equal(TrendAvailability.EpsilonUnknown, Window(noEpsilon, 6).Availability);
    }

    [Fact]
    public void Metadata_mismatch_blocks_comparison_and_suspect_is_visible()
    {
        var incompatible = TrendEngine.Calculate(
            [Sample(-6, 7.32m) with { Datum = "other" }, Sample(0, 7.48m)],
            Hourly, Now);
        var suspect = TrendEngine.Calculate(
            [Sample(-7, 7.32m), Sample(-1, 7.48m),
             Sample(0, 8.00m) with { Quality = TrendSampleQuality.Suspect }],
            Hourly, Now);

        Assert.Equal(TrendAvailability.Incompatible, Window(incompatible, 6).Availability);
        var changedInside = TrendEngine.Calculate(
            [Sample(-6, 7.32m), Sample(-3, 7.40m) with { Epoch = 2 },
             Sample(0, 7.48m)], Hourly, Now);
        Assert.Equal(TrendAvailability.Incompatible, Window(changedInside, 6).Availability);
        Assert.True(suspect.HasNewerSuspect);
        Assert.True(suspect.HasRecentSuspect);
        Assert.Equal(Now.AddHours(-1), suspect.LatestObservedAt);
        Assert.False(suspect.CanTriggerCalculatedRule);

        var interiorSuspect = TrendEngine.Calculate(
            [Sample(-6, 7.32m),
             Sample(-3, 7.40m) with { Quality = TrendSampleQuality.Suspect },
             Sample(0, 7.48m)], Hourly, Now);
        Assert.False(interiorSuspect.HasNewerSuspect);
        Assert.True(interiorSuspect.HasRecentSuspect);
        Assert.False(interiorSuspect.CanTriggerCalculatedRule);
    }

    [Fact]
    public void Negative_levels_duplicates_and_interior_gaps_are_kept_explicit()
    {
        var earlier = Sample(-6, -0.20m);
        var current = Sample(0, -0.10m);
        var snapshot = TrendEngine.Calculate(
            [current, earlier, Sample(-5, -0.18m), earlier], Hourly, Now);

        Assert.Equal(0.10m, Window(snapshot, 6).Delta);
        Assert.Equal(TrendDirection.Rising, Window(snapshot, 6).Direction);
        Assert.True(Window(snapshot, 6).InteriorGaps);
        Assert.Throws<InvalidDataException>(() => TrendEngine.Calculate(
            [earlier, earlier with { Value = 3m }], Hourly, Now));
    }

    [Fact]
    public void One_reading_cannot_be_called_stable_and_windows_can_disagree()
    {
        var single = TrendEngine.Calculate([Sample(0, 7.48m)], Hourly, Now);
        Assert.All(single.Windows, result =>
            Assert.Equal(TrendAvailability.InsufficientData, result.Availability));

        var mixed = TrendEngine.Calculate(
            [Sample(-24, 7.32m), Sample(-1, 7.51m), Sample(0, 7.48m)], Hourly, Now);
        Assert.Equal(TrendDirection.Falling, Window(mixed, 1).Direction);
        Assert.Equal(TrendDirection.Rising, Window(mixed, 24).Direction);
    }

    [Fact]
    public void Revisions_and_time_zone_representation_do_not_change_the_instant()
    {
        var old = Sample(-6, 7.30m);
        var corrected = old with
        {
            Value = 7.32m,
            Revision = 2,
            ObservedAt = old.ObservedAt.ToOffset(TimeSpan.FromHours(-3))
        };
        var snapshot = TrendEngine.Calculate(
            [old, corrected, Sample(0, 7.48m)], Hourly, Now);

        Assert.Equal(0.16m, Window(snapshot, 6).Delta);
        Assert.Equal(Now.AddHours(-6), Window(snapshot, 6).ReferenceAt);
    }

    [Fact]
    public void Unknown_unit_and_future_only_input_do_not_produce_a_current_trend()
    {
        var wrongUnit = TrendEngine.Calculate(
            [Sample(-6, 732m) with { Unit = "cm" }, Sample(0, 7.48m)],
            Hourly, Now);
        var future = TrendEngine.Calculate([Sample(1, 7.48m)], Hourly, Now);

        Assert.Equal(TrendAvailability.Incompatible, Window(wrongUnit, 6).Availability);
        Assert.Equal(TrendFreshness.NoData, future.Freshness);
        Assert.Null(future.PrimaryWindowHours);
    }

    private static TrendSample Sample(double hoursFromNow, decimal value) =>
        new(Now.AddHours(hoursFromNow), value, "m", "synthetic-datum", 1,
            "synthetic-direct", TrendSampleQuality.Accepted, 1);

    private static TrendWindowResult Window(TrendSnapshot snapshot, int hours) =>
        Assert.Single(snapshot.Windows, window => window.WindowHours == hours);
}
