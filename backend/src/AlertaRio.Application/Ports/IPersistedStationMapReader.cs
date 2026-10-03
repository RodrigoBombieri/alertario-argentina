using AlertaRio.Application.PublicData;

namespace AlertaRio.Application.Ports;

public sealed record StationBoundingBox(
    double West, double South, double East, double North);

public interface IPersistedStationMapReader
{
    Task<IReadOnlyList<StationMapPointDto>> GetStationsAsync(
        StationBoundingBox box, int limit, CancellationToken cancellationToken = default);
}
