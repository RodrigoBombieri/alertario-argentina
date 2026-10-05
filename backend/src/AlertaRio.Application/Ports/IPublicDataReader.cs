using AlertaRio.Application.PublicData;

namespace AlertaRio.Application.Ports;

public interface IPublicDataReader
{
    bool IsConfigured { get; }
    bool IsSynthetic { get; }
    bool IsCollecting { get; }
    IReadOnlyList<LocationDto> SearchLocations(string query, int limit) =>
        throw new NotSupportedException("Use the asynchronous public-data reader.");
    LocationDto? GetLocation(string id) =>
        throw new NotSupportedException("Use the asynchronous public-data reader.");
    IReadOnlyList<StationDto> GetStationsForLocation(string locationId) =>
        throw new NotSupportedException("Use the asynchronous public-data reader.");
    IReadOnlyList<StationDto> ListStations(int limit) =>
        throw new NotSupportedException("Use the asynchronous public-data reader.");
    StationDto? GetStation(string id) =>
        throw new NotSupportedException("Use the asynchronous public-data reader.");
    StationSummaryDto? GetSummary(string stationId) =>
        throw new NotSupportedException("Use the asynchronous public-data reader.");
    NoticeListDto GetNoticesForLocation(string locationId) =>
        throw new NotSupportedException("Use the asynchronous public-data reader.");
    IReadOnlyList<SourceDto> ListSources() =>
        throw new NotSupportedException("Use the asynchronous public-data reader.");

    Task<bool> HasOfficialDataAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(IsConfigured && !IsSynthetic && !IsCollecting);
    Task<bool> CanReadSeriesAsync(string seriesId,
        CancellationToken cancellationToken = default) => Task.FromResult(true);
    Task<IReadOnlyList<LocationDto>> SearchLocationsAsync(string query, int limit,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(SearchLocations(query, limit));
    Task<LocationDto?> GetLocationAsync(string id,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(GetLocation(id));
    Task<IReadOnlyList<StationDto>> GetStationsForLocationAsync(string locationId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(GetStationsForLocation(locationId));
    Task<IReadOnlyList<StationDto>> ListStationsAsync(int limit,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ListStations(limit));
    Task<StationDto?> GetStationAsync(string id,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(GetStation(id));
    Task<StationSummaryDto?> GetSummaryAsync(string stationId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(GetSummary(stationId));
    Task<NoticeListDto> GetNoticesForLocationAsync(string locationId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(GetNoticesForLocation(locationId));
    Task<IReadOnlyList<SourceDto>> ListSourcesAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ListSources());
}
