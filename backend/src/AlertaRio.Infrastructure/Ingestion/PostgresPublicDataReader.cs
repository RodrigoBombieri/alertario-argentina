using AlertaRio.Application.Ports;
using AlertaRio.Application.PublicData;
using AlertaRio.Infrastructure.Providers;
using Npgsql;
using NpgsqlTypes;

namespace AlertaRio.Infrastructure.Ingestion;

// Activated explicitly after source rights, station associations and series
// publication have been reviewed. All queries re-evaluate those DB gates.
public sealed class PostgresPublicDataReader(
    NpgsqlDataSource dataSource, IPersistedSummaryReader summaries) : IPublicDataReader
{
    private const string EligibleStationSql = """
        SELECT DISTINCT ON (st.id)
               st.id, st.name, COALESCE(r.name, ''), loc.id,
               chosen.source_id,
               ARRAY(
                   SELECT ms.id FROM measurement_series AS ms
                   JOIN data_sources AS src ON src.id = ms.source_id
                   WHERE ms.station_id = st.id AND ms.approved AND
                         ms.data_kind = 'observed' AND ms.support_seconds = 0 AND
                         src.permission_status = 'approved'
                   ORDER BY ms.id
               )
        FROM stations AS st
        LEFT JOIN rivers AS r ON r.id = st.river_id
        JOIN location_station_associations AS assoc ON assoc.station_id = st.id
        JOIN locations AS loc ON loc.id = assoc.location_id
        JOIN active_catalogs AS ac ON ac.source_id = loc.source_id AND
            ac.catalog_version = loc.catalog_version
        JOIN data_sources AS geo ON geo.id = loc.source_id AND
            geo.permission_status = 'approved'
        JOIN LATERAL (
            SELECT ms.source_id FROM measurement_series AS ms
            JOIN data_sources AS src ON src.id = ms.source_id
            WHERE ms.station_id = st.id AND ms.approved AND
                  ms.data_kind = 'observed' AND ms.support_seconds = 0 AND
                  src.permission_status = 'approved'
            ORDER BY ms.id LIMIT 1
        ) AS chosen ON true
        WHERE st.active AND assoc.status = 'approved' AND
              assoc.valid_from <= now() AND
              (assoc.valid_to IS NULL OR assoc.valid_to > now()) AND
              ($1::uuid IS NULL OR st.id = $1) AND
              ($2::uuid IS NULL OR loc.id = $2)
        ORDER BY st.id, loc.id
        LIMIT $3
        """;

    public bool IsConfigured => true;
    public bool IsSynthetic => false;
    public bool IsCollecting => false;

    public async Task<bool> HasOfficialDataAsync(
        CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT EXISTS (
                SELECT 1 FROM publishable_measurements AS p
                JOIN measurement_series AS ms ON ms.id = p.series_id
                JOIN stations AS st ON st.id = ms.station_id AND st.active
                WHERE EXISTS (
                    SELECT 1 FROM location_station_associations AS assoc
                    JOIN locations AS loc ON loc.id = assoc.location_id
                    JOIN active_catalogs AS ac ON ac.source_id = loc.source_id AND
                        ac.catalog_version = loc.catalog_version
                    JOIN data_sources AS geo ON geo.id = loc.source_id AND
                        geo.permission_status = 'approved'
                    WHERE assoc.station_id = st.id AND assoc.status = 'approved' AND
                          assoc.valid_from <= now() AND
                          (assoc.valid_to IS NULL OR assoc.valid_to > now())
                )
            )
            """);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    public async Task<bool> CanReadSeriesAsync(string seriesId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(seriesId, out var id)) return false;
        await using var command = dataSource.CreateCommand("""
            SELECT EXISTS (
                SELECT 1 FROM measurement_series AS ms
                JOIN data_sources AS src ON src.id = ms.source_id AND
                    src.permission_status = 'approved'
                JOIN stations AS st ON st.id = ms.station_id AND st.active
                WHERE ms.id = $1 AND ms.approved AND ms.data_kind = 'observed' AND
                      ms.support_seconds = 0 AND EXISTS (
                    SELECT 1 FROM location_station_associations AS assoc
                    JOIN locations AS loc ON loc.id = assoc.location_id
                    JOIN active_catalogs AS ac ON ac.source_id = loc.source_id AND
                        ac.catalog_version = loc.catalog_version
                    JOIN data_sources AS geo ON geo.id = loc.source_id AND
                        geo.permission_status = 'approved'
                    WHERE assoc.station_id = st.id AND assoc.status = 'approved' AND
                          assoc.valid_from <= now() AND
                          (assoc.valid_to IS NULL OR assoc.valid_to > now())
                )
            )
            """);
        command.Parameters.Add(new NpgsqlParameter { Value = id });
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    public async Task<IReadOnlyList<LocationDto>> SearchLocationsAsync(
        string query, int limit, CancellationToken cancellationToken = default)
    {
        var normalized = GeoRefLocalitySearch.Normalize(query);
        if (normalized.Length < 2) return [];
        await using var command = dataSource.CreateCommand("""
            SELECT loc.id, loc.name, loc.province_id, loc.province_name
            FROM locations AS loc
            JOIN active_catalogs AS ac ON ac.source_id = loc.source_id AND
                ac.catalog_version = loc.catalog_version
            JOIN data_sources AS src ON src.id = loc.source_id AND
                src.permission_status = 'approved'
            WHERE position($1 in upper(loc.normalized_name)) > 0
            ORDER BY loc.name, loc.province_name, loc.id LIMIT $2
            """);
        command.Parameters.Add(new NpgsqlParameter { Value = normalized });
        command.Parameters.Add(new NpgsqlParameter { Value = limit });
        var locations = new List<LocationDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            locations.Add(new LocationDto(reader.GetGuid(0).ToString("D"),
                reader.GetString(1), reader.GetString(2), reader.GetString(3), false));
        return locations;
    }

    public async Task<LocationDto?> GetLocationAsync(string id,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var locationId)) return null;
        await using var command = dataSource.CreateCommand("""
            SELECT loc.id, loc.name, loc.province_id, loc.province_name
            FROM locations AS loc
            JOIN active_catalogs AS ac ON ac.source_id = loc.source_id AND
                ac.catalog_version = loc.catalog_version
            JOIN data_sources AS src ON src.id = loc.source_id AND
                src.permission_status = 'approved'
            WHERE loc.id = $1
            """);
        command.Parameters.Add(new NpgsqlParameter { Value = locationId });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new LocationDto(reader.GetGuid(0).ToString("D"), reader.GetString(1),
                reader.GetString(2), reader.GetString(3), false)
            : null;
    }

    public Task<IReadOnlyList<StationDto>> GetStationsForLocationAsync(
        string locationId, CancellationToken cancellationToken = default) =>
        QueryStationsAsync(null, Guid.TryParse(locationId, out var id) ? id : Guid.Empty,
            50, cancellationToken);

    public Task<IReadOnlyList<StationDto>> ListStationsAsync(int limit,
        CancellationToken cancellationToken = default) =>
        QueryStationsAsync(null, null, limit, cancellationToken);

    public async Task<StationDto?> GetStationAsync(string id,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var stationId)) return null;
        var stations = await QueryStationsAsync(stationId, null, 1, cancellationToken);
        return stations.Count == 0 ? null : stations[0];
    }

    private async Task<IReadOnlyList<StationDto>> QueryStationsAsync(
        Guid? stationId, Guid? locationId, int limit, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(EligibleStationSql);
        command.Parameters.Add(new NpgsqlParameter
        {
            NpgsqlDbType = NpgsqlDbType.Uuid,
            Value = stationId is null ? DBNull.Value : stationId.Value
        });
        command.Parameters.Add(new NpgsqlParameter
        {
            NpgsqlDbType = NpgsqlDbType.Uuid,
            Value = locationId is null ? DBNull.Value : locationId.Value
        });
        command.Parameters.Add(new NpgsqlParameter { Value = limit });
        var stations = new List<StationDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            stations.Add(new StationDto(reader.GetGuid(0).ToString("D"),
                reader.GetString(1), reader.GetString(2),
                reader.GetGuid(3).ToString("D"), reader.GetGuid(4).ToString("D"),
                reader.GetFieldValue<Guid[]>(5).Select(id => id.ToString("D")).ToArray(),
                false));
        return stations;
    }

    public Task<StationSummaryDto?> GetSummaryAsync(string stationId,
        CancellationToken cancellationToken = default) =>
        summaries.GetSummaryAsync(stationId, cancellationToken);

    public Task<NoticeListDto> GetNoticesForLocationAsync(string locationId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new NoticeListDto(false, [],
            new NoticeCoverageDto("notConfigured", null)));

    public async Task<IReadOnlyList<SourceDto>> ListSourcesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT src.id, src.name, src.code,
                   COALESCE(src.owner_name, src.name),
                   COALESCE(src.license_url, src.rights_decision_id, '')
            FROM data_sources AS src
            WHERE src.permission_status = 'approved' AND
                  (EXISTS (SELECT 1 FROM active_catalogs AS ac
                           WHERE ac.source_id = src.id) OR
                   EXISTS (SELECT 1 FROM measurement_series AS ms
                           WHERE ms.source_id = src.id AND ms.approved))
            ORDER BY src.name, src.id
            """);
        var sources = new List<SourceDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            sources.Add(new SourceDto(reader.GetGuid(0).ToString("D"),
                reader.GetString(1), reader.GetString(2).StartsWith("ina-a5:",
                    StringComparison.Ordinal) ? "hydrology" : "geography",
                reader.GetString(3), reader.GetString(4), false));
        return sources;
    }
}
