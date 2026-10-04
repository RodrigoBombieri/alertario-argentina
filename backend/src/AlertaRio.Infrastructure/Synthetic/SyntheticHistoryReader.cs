using AlertaRio.Application.Ports;
using AlertaRio.Application.PublicData;

namespace AlertaRio.Infrastructure.Synthetic;

public sealed class SyntheticHistoryReader(TimeProvider clock) : IPersistedHistoryReader
{
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
