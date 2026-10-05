using System.Text;
using AlertaRio.Infrastructure.Providers;
using Npgsql;
using NpgsqlTypes;

namespace AlertaRio.Infrastructure.Ingestion;

public sealed class PostgresGeoRefCatalogStore(NpgsqlDataSource dataSource)
{
    public async Task<bool> CanImportAsync(Guid sourceId, string rightsDecisionId,
        CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT EXISTS (
                SELECT 1 FROM data_sources
                WHERE id = $1 AND code LIKE 'georef%' AND
                      permission_status = 'approved' AND rights_decision_id = $2
            )
            """);
        command.Parameters.Add(new NpgsqlParameter { Value = sourceId });
        command.Parameters.Add(new NpgsqlParameter { Value = rightsDecisionId });
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    // An initial complete snapshot is activated. Later versions remain complete
    // but inactive until their locality-station associations are reviewed.
    public async Task<bool> ImportAsync(Guid sourceId, string rightsDecisionId,
        string version, GeoRefCatalogSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (sourceId == Guid.Empty || string.IsNullOrWhiteSpace(rightsDecisionId) ||
            string.IsNullOrWhiteSpace(version) || version.Length > 100 ||
            snapshot.Total <= 0 || snapshot.Localities.Count != snapshot.Total ||
            snapshot.Localities.Select(item => item.ExternalId)
                .Distinct(StringComparer.Ordinal).Count() != snapshot.Total)
            throw new ArgumentException("GeoRef import requires a complete, versioned snapshot.");

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var gate = new NpgsqlCommand("""
            SELECT code, permission_status, rights_decision_id
            FROM data_sources WHERE id = $1 FOR UPDATE
            """, connection, transaction))
        {
            gate.Parameters.Add(new NpgsqlParameter { Value = sourceId });
            await using var reader = await gate.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) ||
                !reader.GetString(0).StartsWith("georef", StringComparison.Ordinal) ||
                reader.GetString(1) != "approved" || reader.IsDBNull(2) ||
                reader.GetString(2) != rightsDecisionId)
                throw new InvalidOperationException("GeoRef catalog rights are not approved.");
        }

        await using (var existing = new NpgsqlCommand("""
            SELECT EXISTS (SELECT 1 FROM catalog_snapshots
                           WHERE source_id = $1 AND version = $2)
            """, connection, transaction))
        {
            existing.Parameters.Add(new NpgsqlParameter { Value = sourceId });
            existing.Parameters.Add(new NpgsqlParameter { Value = version });
            if ((bool)(await existing.ExecuteScalarAsync(cancellationToken) ?? false))
            {
                await transaction.CommitAsync(cancellationToken);
                return false;
            }
        }

        await using (var create = new NpgsqlCommand("""
            INSERT INTO catalog_snapshots (source_id, version, status, row_count)
            VALUES ($1, $2, 'staging', $3)
            """, connection, transaction))
        {
            create.Parameters.Add(new NpgsqlParameter { Value = sourceId });
            create.Parameters.Add(new NpgsqlParameter { Value = version });
            create.Parameters.Add(new NpgsqlParameter { Value = snapshot.Total });
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        const int batchSize = 100;
        for (var offset = 0; offset < snapshot.Total; offset += batchSize)
        {
            var batch = snapshot.Localities.Skip(offset).Take(batchSize).ToArray();
            var sql = new StringBuilder("""
                INSERT INTO locations
                    (id, source_id, external_id, catalog_version, name,
                     normalized_name, category, province_id, province_name, point)
                VALUES
                """);
            await using var command = new NpgsqlCommand
            {
                Connection = connection,
                Transaction = transaction
            };
            for (var index = 0; index < batch.Length; index++)
            {
                if (index > 0) sql.Append(',');
                var start = index * 11;
                string Parameter(int number) => $"${start + number}";
                sql.Append('(').Append(Parameter(1)).Append(',').Append(Parameter(2))
                    .Append(',').Append(Parameter(3)).Append(',').Append(Parameter(4))
                    .Append(',').Append(Parameter(5)).Append(',').Append(Parameter(6))
                    .Append(',').Append(Parameter(7)).Append(',').Append(Parameter(8))
                    .Append(',').Append(Parameter(9)).Append(", CASE WHEN ")
                    .Append(Parameter(10)).Append("::double precision IS NULL THEN NULL ELSE ")
                    .Append("ST_SetSRID(ST_MakePoint(").Append(Parameter(10))
                    .Append(',').Append(Parameter(11))
                    .Append("), 4326)::geography END)");
                var locality = batch[index];
                command.Parameters.Add(new NpgsqlParameter { Value = Guid.NewGuid() });
                command.Parameters.Add(new NpgsqlParameter { Value = sourceId });
                command.Parameters.Add(new NpgsqlParameter { Value = locality.ExternalId });
                command.Parameters.Add(new NpgsqlParameter { Value = version });
                command.Parameters.Add(new NpgsqlParameter { Value = locality.Name });
                command.Parameters.Add(new NpgsqlParameter
                {
                    Value = GeoRefLocalitySearch.Normalize(locality.Name)
                });
                command.Parameters.Add(new NpgsqlParameter { Value = locality.Category });
                command.Parameters.Add(new NpgsqlParameter { Value = locality.ProvinceId });
                command.Parameters.Add(new NpgsqlParameter { Value = locality.ProvinceName });
                command.Parameters.Add(new NpgsqlParameter
                {
                    NpgsqlDbType = NpgsqlDbType.Double,
                    Value = locality.Longitude is null ? DBNull.Value : locality.Longitude.Value
                });
                command.Parameters.Add(new NpgsqlParameter
                {
                    NpgsqlDbType = NpgsqlDbType.Double,
                    Value = locality.Latitude is null ? DBNull.Value : locality.Latitude.Value
                });
            }
            command.CommandText = sql.ToString();
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var active = new NpgsqlCommand("""
            SELECT EXISTS (SELECT 1 FROM active_catalogs WHERE source_id = $1)
            """, connection, transaction))
        {
            active.Parameters.Add(new NpgsqlParameter { Value = sourceId });
            var hasActive = (bool)(await active.ExecuteScalarAsync(cancellationToken) ?? false);
            await using var verify = new NpgsqlCommand("""
                SELECT count(*) FROM locations
                WHERE source_id = $1 AND catalog_version = $2
                """, connection, transaction);
            verify.Parameters.Add(new NpgsqlParameter { Value = sourceId });
            verify.Parameters.Add(new NpgsqlParameter { Value = version });
            if ((long)(await verify.ExecuteScalarAsync(cancellationToken) ?? -1L) !=
                snapshot.Total)
                throw new InvalidOperationException("GeoRef catalog staging was incomplete.");
            await using var finish = new NpgsqlCommand(hasActive
                ? "UPDATE catalog_snapshots SET status = 'complete' " +
                  "WHERE source_id = $1 AND version = $2"
                : "SELECT activate_catalog_snapshot($1, $2)",
                connection, transaction);
            finish.Parameters.Add(new NpgsqlParameter { Value = sourceId });
            finish.Parameters.Add(new NpgsqlParameter { Value = version });
            await finish.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
