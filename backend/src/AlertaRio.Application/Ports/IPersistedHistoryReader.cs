using AlertaRio.Application.PublicData;

namespace AlertaRio.Application.Ports;

public interface IPersistedHistoryReader
{
    Task<SeriesHistoryDto?> GetRecentAsync(
        string seriesId, CancellationToken cancellationToken = default);

    Task<SeriesHistoryPageDto?> GetPageAsync(
        string seriesId, DateTimeOffset from, DateTimeOffset to,
        DateTimeOffset? before, int limit,
        CancellationToken cancellationToken = default);
}
