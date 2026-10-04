using AlertaRio.Application.Ports;
using AlertaRio.Application.PublicData;

namespace AlertaRio.Infrastructure.Synthetic;

public sealed class SyntheticHistoryReader(TimeProvider clock) : IPersistedHistoryReader
{
    public Task<SeriesHistoryPageDto?> GetPageAsync(
        string seriesId, DateTimeOffset from, DateTimeOffset to,
        DateTimeOffset? before, int limit,
        CancellationToken cancellationToken = default)
    {
        if (from >= to || to - from > TimeSpan.FromDays(31) ||
            before is not null && (before <= from || before > to) ||
            limit is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(from));
        if (seriesId != SyntheticPublicDataReader.SeriesId)
            return Task.FromResult<SeriesHistoryPageDto?>(null);
        var ordered = BuildPoints(clock).Where(point => point.ObservedAt >= from &&
                point.ObservedAt < to &&
                (before is null || point.ObservedAt < before))
            .OrderByDescending(point => point.ObservedAt)
            .Take(limit + 1).ToArray();
        var hasMore = ordered.Length > limit;
        var points = hasMore ? ordered[..limit] : ordered;
        return Task.FromResult<SeriesHistoryPageDto?>(new SeriesHistoryPageDto(seriesId, "m",
            3600, clock.GetUtcNow(), true, from, to,
            hasMore ? points[^1].ObservedAt.ToUniversalTime().ToString("O") : null,
            points));
    }

    public Task<SeriesHistoryDto?> GetRecentAsync(
        string seriesId, CancellationToken cancellationToken = default)
    {
        if (seriesId != SyntheticPublicDataReader.SeriesId)
            return Task.FromResult<SeriesHistoryDto?>(null);
        return Task.FromResult<SeriesHistoryDto?>(new SeriesHistoryDto(seriesId,
            "m", 3600, clock.GetUtcNow(), true, false,
            BuildPoints(clock).TakeLast(25).ToArray()));
    }

    internal static DateTimeOffset LatestObservationAt(TimeProvider clock)
    {
        var now = clock.GetUtcNow();
        return new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0,
            TimeSpan.Zero).AddMinutes(-10);
    }

    private static HistoryPointDto[] BuildPoints(TimeProvider clock)
    {
        var latest = LatestObservationAt(clock);
        return Enumerable.Range(0, 337).Reverse().Select(hoursAgo =>
        {
            var observedAt = latest.AddHours(-hoursAgo);
            var value = 7.48m - hoursAgo % 24 * 0.01m;
            return new HistoryPointDto(observedAt, value, observedAt,
                observedAt.AddMinutes(2), 1);
        }).ToArray();
    }
}
