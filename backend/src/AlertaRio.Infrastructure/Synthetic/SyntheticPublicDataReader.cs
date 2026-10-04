using AlertaRio.Application.Ports;
using AlertaRio.Application.PublicData;
using AlertaRio.Core;

namespace AlertaRio.Infrastructure.Synthetic;

public sealed class SyntheticPublicDataReader(TimeProvider clock) : IPublicDataReader
{
    public const string LocationId = "location-demo";
    public const string StationId = "station-demo";
    public const string SeriesId = "series-demo-height";
    private const string SourceId = "source-demo";

    private static readonly LocationDto Location = new(
        LocationId, "Localidad de ejemplo", "province-demo", "Provincia de ejemplo", true);

    private static readonly StationDto Station = new(
        StationId, "Estación de ejemplo", "Río de ejemplo", LocationId, SourceId,
        [SeriesId], true);

    private static readonly SourceDto Source = new(
        SourceId, "Datos sintéticos de AlertaRío", DataProvenance.Synthetic.ToString(),
        "AlertaRío · fixture sintética", "No aplica a datos oficiales", true);

    public bool IsConfigured => true;
    public bool IsSynthetic => true;

    public IReadOnlyList<LocationDto> SearchLocations(string query, int limit) =>
        Location.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ? [Location] : [];

    public LocationDto? GetLocation(string id) => id == LocationId ? Location : null;

    public IReadOnlyList<StationDto> GetStationsForLocation(string locationId) =>
        locationId == LocationId ? [Station] : [];

    public IReadOnlyList<StationDto> ListStations(int limit) => [Station];

    public StationDto? GetStation(string id) => id == StationId ? Station : null;

    public StationSummaryDto? GetSummary(string stationId)
    {
        if (stationId != StationId) return null;

        var observationTime = clock.GetUtcNow().AddMinutes(-10);
        var height = new MeasurementDto(
            SeriesId, 7.48m, "m", observationTime,
            observationTime.AddMinutes(2), observationTime.AddMinutes(4),
            "synthetic", "notApplicable", SourceId);
        int[] windows = [1, 3, 6, 12, 24];
        var changes = windows.Select(hours => new ChangeDto(
            hours, null, "m", null, null, "observedEndpoints", "unknown",
            "unavailable", "insufficientObservations")).ToArray();

        return new StationSummaryDto(
            StationId, clock.GetUtcNow(), true, height, null, "noApprovedSeries",
            changes, "notApplicable", "notCalculated", [], [],
            new NoticeCoverageDto("notConfigured", null),
            "notApplied", "synthetic-v1");
    }

    public IReadOnlyList<SourceDto> ListSources() => [Source];

    public NoticeListDto GetNoticesForLocation(string locationId) =>
        new(true, [], new NoticeCoverageDto("notConfigured", null));
}
