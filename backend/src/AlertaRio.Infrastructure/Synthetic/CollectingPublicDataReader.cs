using AlertaRio.Application.Ports;
using AlertaRio.Application.PublicData;

namespace AlertaRio.Infrastructure.Synthetic;

// Cold-start preview: demo values are synthetic and never imply official notice coverage.
public sealed class CollectingPublicDataReader(TimeProvider clock) : IPublicDataReader
{
    private readonly SyntheticPublicDataReader demo = new(clock);
    public bool IsConfigured => true;
    public bool IsSynthetic => true;
    public bool IsCollecting => true;
    public IReadOnlyList<LocationDto> SearchLocations(string query, int limit) =>
        demo.SearchLocations(query, limit);
    public LocationDto? GetLocation(string id) => demo.GetLocation(id);
    public IReadOnlyList<StationDto> GetStationsForLocation(string locationId) =>
        demo.GetStationsForLocation(locationId);
    public IReadOnlyList<StationDto> ListStations(int limit) => demo.ListStations(limit);
    public StationDto? GetStation(string id) => demo.GetStation(id);
    public StationSummaryDto? GetSummary(string stationId) => demo.GetSummary(stationId);
    public NoticeListDto GetNoticesForLocation(string locationId) =>
        demo.GetNoticesForLocation(locationId);
    public IReadOnlyList<SourceDto> ListSources() => demo.ListSources();
}
