using AlertaRio.Application.Ports;
using AlertaRio.Application.PublicData;
using Npgsql;

namespace AlertaRio.Infrastructure.Ingestion;

// Development preview only: no station state is inferred from map position.
public sealed class PostgresStationMapReader(NpgsqlDataSource dataSource)
    : IPersistedStationMapReader
{
    public async Task<IReadOnlyList<StationMapPointDto>> GetStationsAsync(
        StationBoundingBox box, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(box);
        if (limit is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(limit));
        await using var command = dataSource.CreateCommand("""
            SELECT st.id, st.name, COALESCE(r.name, ''),
                   ST_X(st.point::geometry), ST_Y(st.point::geometry)
            FROM stations AS st
            LEFT JOIN rivers AS r ON r.id = st.river_id
            WHERE st.active AND st.point IS NOT NULL AND
                  st.point::geometry && ST_MakeEnvelope($1, $2, $3, $4, 4326) AND
                  EXISTS (
                    SELECT 1 FROM measurement_series AS ms
                    JOIN data_sources AS d ON d.id = ms.source_id
                    WHERE ms.station_id = st.id AND ms.approved AND
                          ms.data_kind = 'observed' AND ms.support_seconds = 0 AND
                          d.permission_status = 'approved'
                  )
            ORDER BY st.id LIMIT $5
            """);
        command.Parameters.Add(new NpgsqlParameter { Value = box.West });
        command.Parameters.Add(new NpgsqlParameter { Value = box.South });
        command.Parameters.Add(new NpgsqlParameter { Value = box.East });
        command.Parameters.Add(new NpgsqlParameter { Value = box.North });
        command.Parameters.Add(new NpgsqlParameter { Value = limit });
        var stations = new List<StationMapPointDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            stations.Add(new StationMapPointDto(reader.GetGuid(0).ToString("D"),
                reader.GetString(1), reader.GetString(2), reader.GetDouble(3),
                reader.GetDouble(4), true));
        return stations;
    }
}
