namespace AlertaRio.Core.Trends;

public enum TrendSampleQuality { Accepted, Suspect }
public enum TrendFreshness { NoData, Unknown, Fresh, Stale }
public enum TrendDirection { Unavailable, Rising, Falling, Stable }
public enum TrendAvailability { InsufficientData, Incompatible, FreshnessUnknown, HistoricalOnly, EpsilonUnknown, Available }

public sealed record TrendSample(
    DateTimeOffset ObservedAt, decimal Value, string Unit, string? Datum,
    int Epoch, string Procedure, TrendSampleQuality Quality, int Revision);

public sealed record TrendPolicy(
    TimeSpan? Cadence, TimeSpan? AllowedLag, decimal? Epsilon,
    string MethodologyVersion);

public sealed record TrendWindowResult(
    int WindowHours, decimal? Delta, DateTimeOffset? ReferenceAt,
    int? ActualDurationSeconds, TrendDirection Direction,
    TrendAvailability Availability, string? Reason, bool? InteriorGaps);

public sealed record TrendSnapshot(
    DateTimeOffset? LatestObservedAt, decimal? LatestValue,
    string? LatestUnit, string? LatestDatum, int? LatestEpoch,
    TrendFreshness Freshness,
    bool HasNewerSuspect, bool HasRecentSuspect,
    IReadOnlyList<TrendWindowResult> Windows,
    int? PrimaryWindowHours, string MethodologyVersion)
{
    public bool CanTriggerCalculatedRule => PrimaryWindowHours is not null &&
        Freshness == TrendFreshness.Fresh && !HasRecentSuspect;
}

// Calculates comparisons only; it never interprets a rise as an official warning.
public static class TrendEngine
{
    private static readonly int[] Windows = [1, 3, 6, 12, 24];

    public static TrendSnapshot Calculate(
        IReadOnlyList<TrendSample> samples, TrendPolicy policy, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(policy);
        if (policy.Cadence is { } cadence && cadence <= TimeSpan.Zero ||
            policy.AllowedLag is { } lag && lag < TimeSpan.Zero ||
            policy.Epsilon is { } epsilon && epsilon < 0 ||
            string.IsNullOrWhiteSpace(policy.MethodologyVersion))
            throw new ArgumentException("Trend policy contains invalid parameters.", nameof(policy));

        var normalized = samples.GroupBy(sample => sample.ObservedAt.ToUniversalTime())
            .Select(group =>
            {
                var topRevision = group.Max(sample => sample.Revision);
                var latest = group.Where(sample => sample.Revision == topRevision).ToArray();
                if (topRevision < 1 || latest.Any(sample =>
                    sample.Value != latest[0].Value || sample.Unit != latest[0].Unit ||
                    sample.Datum != latest[0].Datum || sample.Epoch != latest[0].Epoch ||
                    sample.Procedure != latest[0].Procedure ||
                    sample.Quality != latest[0].Quality))
                    throw new InvalidDataException("Conflicting measurement revisions.");
                return latest[0] with { ObservedAt = group.Key };
            })
            .Where(sample => sample.ObservedAt <= now)
            .OrderBy(sample => sample.ObservedAt)
            .ToArray();
        var accepted = normalized.Where(sample => sample.Quality == TrendSampleQuality.Accepted)
            .ToArray();
        var current = accepted.LastOrDefault();
        if (current is null)
            return new TrendSnapshot(null, null, null, null, null,
                TrendFreshness.NoData, normalized.Length > 0,
                normalized.Any(sample => sample.Quality == TrendSampleQuality.Suspect),
                Windows.Select(window => Unavailable(window, TrendAvailability.InsufficientData,
                    "noAcceptedMeasurement")).ToArray(), null, policy.MethodologyVersion);

        var freshness = policy.Cadence is null || policy.AllowedLag is null
            ? TrendFreshness.Unknown
            : now - current.ObservedAt > policy.Cadence.Value + policy.AllowedLag.Value
                ? TrendFreshness.Stale : TrendFreshness.Fresh;
        var hasNewerSuspect = normalized.Any(sample =>
            sample.Quality == TrendSampleQuality.Suspect &&
            sample.ObservedAt > current.ObservedAt);
        var hasRecentSuspect = normalized.Any(sample =>
            sample.Quality == TrendSampleQuality.Suspect &&
            sample.ObservedAt >= current.ObservedAt.AddHours(-24));
        var results = Windows.Select(window => CalculateWindow(
            accepted, current, window, policy, freshness)).ToArray();
        var primary = results.FirstOrDefault(result =>
            result.WindowHours is 6 or 12 or 24 &&
            result.Availability == TrendAvailability.Available)?.WindowHours;
        return new TrendSnapshot(current.ObservedAt, current.Value, current.Unit,
            current.Datum, current.Epoch, freshness, hasNewerSuspect,
            hasRecentSuspect,
            results, primary, policy.MethodologyVersion);
    }

    private static TrendWindowResult CalculateWindow(
        IReadOnlyList<TrendSample> accepted, TrendSample current, int hours,
        TrendPolicy policy, TrendFreshness freshness)
    {
        var window = TimeSpan.FromHours(hours);
        var target = current.ObservedAt - window;
        var tolerance = policy.Cadence is { } cadence
            ? TimeSpan.FromTicks(Math.Min(window.Ticks / 10, cadence.Ticks / 2))
            : TimeSpan.Zero;
        var reference = accepted.Where(sample => sample.ObservedAt < current.ObservedAt)
            .OrderBy(sample => Math.Abs((sample.ObservedAt - target).Ticks))
            .ThenBy(sample => sample.ObservedAt)
            .FirstOrDefault();
        if (reference is null || (reference.ObservedAt - target).Duration() > tolerance)
            return Unavailable(hours, TrendAvailability.InsufficientData, "noReferenceInTolerance");
        var segment = accepted.Where(sample => sample.ObservedAt >= reference.ObservedAt &&
            sample.ObservedAt <= current.ObservedAt).ToArray();
        if (string.IsNullOrWhiteSpace(current.Unit) ||
            string.IsNullOrWhiteSpace(current.Datum) ||
            string.IsNullOrWhiteSpace(current.Procedure) ||
            segment.Any(sample => sample.Unit != current.Unit ||
                sample.Datum != current.Datum || sample.Epoch != current.Epoch ||
                sample.Procedure != current.Procedure))
            return Unavailable(hours, TrendAvailability.Incompatible,
                "unitDatumEpochOrProcedureMismatch");

        var delta = current.Value - reference.Value;
        var duration = checked((int)(current.ObservedAt - reference.ObservedAt).TotalSeconds);
        bool? interiorGaps = policy.Cadence is { } approvedCadence
            ? segment.Zip(segment.Skip(1),
                    (left, right) => right.ObservedAt - left.ObservedAt)
                .Any(gap => gap > approvedCadence * 2)
            : null;
        var availability = freshness switch
        {
            TrendFreshness.Stale => TrendAvailability.HistoricalOnly,
            TrendFreshness.Unknown => TrendAvailability.FreshnessUnknown,
            _ when policy.Epsilon is null => TrendAvailability.EpsilonUnknown,
            _ => TrendAvailability.Available
        };
        var direction = availability != TrendAvailability.Available
            ? TrendDirection.Unavailable
            : delta > policy.Epsilon ? TrendDirection.Rising
            : delta < -policy.Epsilon ? TrendDirection.Falling
            : TrendDirection.Stable;
        return new TrendWindowResult(hours, delta, reference.ObservedAt, duration,
            direction, availability, availability == TrendAvailability.Available ? null :
                availability.ToString(), interiorGaps);
    }

    private static TrendWindowResult Unavailable(
        int hours, TrendAvailability availability, string reason) =>
        new(hours, null, null, null, TrendDirection.Unavailable, availability, reason, null);
}
