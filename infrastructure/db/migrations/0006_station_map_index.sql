BEGIN;

SELECT pg_advisory_xact_lock(72651001);

CREATE INDEX stations_point_geometry_gix ON stations
USING gist ((point::geometry)) WHERE active AND point IS NOT NULL;

INSERT INTO schema_migrations(version) VALUES ('0006_station_map_index');
COMMIT;
