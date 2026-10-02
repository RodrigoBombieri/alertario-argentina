using AlertaRio.Application.Ports;
using AlertaRio.Application.PublicData;

namespace AlertaRio.Infrastructure.Synthetic;

public sealed class UnavailablePublicDataReader : IPublicDataReader
{
    public bool IsConfigured => false;
    public bool IsSynthetic => false;
    public IReadOnlyList<LocationDto> SearchLocations(string query, int limit) => [];
    public LocationDto? GetLocation(string id) => null;
    public IReadOnlyList<StationDto> GetStationsForLocation(string locationId) => [];
    public IReadOnlyList<StationDto> ListStations(int limit) => [];
    public StationDto? GetStation(string id) => null;
    public StationSummaryDto? GetSummary(string stationId) => null;
    public NoticeListDto GetNoticesForLocation(string locationId) =>
        new(false, [], new NoticeCoverageDto("unavailable", null));
    public IReadOnlyList<SourceDto> ListSources() => [];
}
