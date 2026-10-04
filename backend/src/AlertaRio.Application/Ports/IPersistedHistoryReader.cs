using AlertaRio.Application.PublicData;

namespace AlertaRio.Application.Ports;

public interface IPersistedHistoryReader
{
    Task<SeriesHistoryDto?> GetRecentAsync(
        string seriesId, CancellationToken cancellationToken = default);
}
