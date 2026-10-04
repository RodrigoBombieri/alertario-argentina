using AlertaRio.Application.Ports;
using AlertaRio.Application.PublicData;

namespace AlertaRio.Infrastructure.Synthetic;

public sealed class SyntheticHistoryReader(TimeProvider clock) : IPersistedHistoryReader
{
    public async Task<SeriesHistoryPageDto?> GetPageAsync(
        string seriesId, DateTimeOffset from, DateTimeOffset to,
        DateTimeOffset? before, int limit,
        CancellationToken cancellationToken = default)
    {
        if (from >= to || to - from > TimeSpan.FromDays(31) ||
            before is not null && (before <= from || before > to) ||
            limit is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(from));
        var recent = await GetRecentAsync(seriesId, cancellationToken);
        if (recent is null) return null;
        var ordered = recent.Points.Where(point => point.ObservedAt >= from &&
                point.ObservedAt < to &&
                (before is null || point.ObservedAt < before))
            .OrderByDescending(point => point.ObservedAt)
            .Take(limit + 1).ToArray();
        var hasMore = ordered.Length > limit;
        var points = hasMore ? ordered[..limit] : ordered;
        return new SeriesHistoryPageDto(seriesId, recent.Unit,
            recent.CadenceSeconds, clock.GetUtcNow(), true, from, to,
            hasMore ? points[^1].ObservedAt.ToUniversalTime().ToString("O") : null,
            points);
    }

    public Task<SeriesHistoryDto?> GetRecentAsync(
        string seriesId, CancellationToken cancellationToken = default)
    {
        if (seriesId != SyntheticPublicDataReader.SeriesId)
            return Task.FromResult<SeriesHistoryDto?>(null);
        var latest = clock.GetUtcNow().AddMinutes(-10);
        return Task.FromResult<SeriesHistoryDto?>(new SeriesHistoryDto(seriesId,
            "m", 3600, clock.GetUtcNow(), true, false,
            [new HistoryPointDto(latest.AddHours(-6), 7.32m),
             new HistoryPointDto(latest.AddHours(-3), 7.40m),
             new HistoryPointDto(latest, 7.48m)]));
    }
}
