using AlertaRio.Application.PublicData;

namespace AlertaRio.Application.Ports;

public interface IPersistedSummaryReader
{
    Task<StationSummaryDto?> GetSummaryAsync(
        string stationId, CancellationToken cancellationToken = default);
}
