using AlertaRio.Application.PublicData;

namespace AlertaRio.Application.Ports;

public interface IPublicDataReader
{
    bool IsConfigured { get; }
    bool IsSynthetic { get; }
    bool IsCollecting { get; }
    IReadOnlyList<LocationDto> SearchLocations(string query, int limit);
    LocationDto? GetLocation(string id);
    IReadOnlyList<StationDto> GetStationsForLocation(string locationId);
    IReadOnlyList<StationDto> ListStations(int limit);
    StationDto? GetStation(string id);
    StationSummaryDto? GetSummary(string stationId);
    NoticeListDto GetNoticesForLocation(string locationId);
    IReadOnlyList<SourceDto> ListSources();
}
