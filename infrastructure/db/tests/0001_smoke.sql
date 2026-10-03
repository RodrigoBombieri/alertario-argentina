BEGIN;

DO $$
DECLARE
    v_source_id uuid := '00000000-0000-4000-8000-000000000001';
    v_station_id uuid := '00000000-0000-4000-8000-000000000002';
    v_series_id uuid := '00000000-0000-4000-8000-000000000003';
    v_locality_id uuid := '00000000-0000-4000-8000-000000000004';
    v_measurement_id bigint;
    rejected boolean;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM schema_migrations WHERE version = '0001_initial') OR
       NOT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'postgis') THEN
        RAISE EXCEPTION 'Initial migration or PostGIS extension is missing';
    END IF;

    INSERT INTO data_sources (id, code, name)
    VALUES (v_source_id, 'synthetic-smoke', 'Synthetic smoke source');
    INSERT INTO catalog_snapshots (source_id, version, status, row_count)
    VALUES (v_source_id, 'smoke-v1', 'staging', 1);

    rejected := false;
    BEGIN
        PERFORM activate_catalog_snapshot(v_source_id, 'smoke-v1');
    EXCEPTION WHEN raise_exception THEN
        rejected := true;
    END;
    IF NOT rejected THEN
        RAISE EXCEPTION 'Incomplete catalog was activated';
    END IF;

    INSERT INTO locations (id, source_id, external_id, catalog_version,
                           name, normalized_name, category, province_id, province_name,
                           point)
    VALUES (v_locality_id, v_source_id, '001', 'smoke-v1',
            'San José', 'SAN JOSE', 'Localidad simple', '30', 'Entre Ríos',
            ST_GeogFromText('SRID=4326;POINT(-58.1 -32.2)'));
    PERFORM activate_catalog_snapshot(v_source_id, 'smoke-v1');
    IF NOT EXISTS (
        SELECT 1 FROM active_catalogs
        WHERE source_id = v_source_id AND catalog_version = 'smoke-v1'
    ) THEN
        RAISE EXCEPTION 'Complete catalog was not activated';
    END IF;

    INSERT INTO stations (id, name, point)
    VALUES (v_station_id, 'Synthetic station',
            ST_GeogFromText('SRID=4326;POINT(-58.2 -32.3)'));
    IF NOT EXISTS (
        SELECT 1 FROM stations
        WHERE id = v_station_id AND ST_DWithin(
            point, ST_GeogFromText('SRID=4326;POINT(-58.2 -32.3)'), 1)
    ) THEN
        RAISE EXCEPTION 'Spatial station point is not queryable';
    END IF;

    rejected := false;
    BEGIN
        INSERT INTO measurement_series (
            id, station_id, source_id, external_id, variable_code, unit_id,
            unit, procedure_name, support_seconds, data_kind, approved)
        VALUES (v_series_id, v_station_id, v_source_id, '31', 'H', 3,
                'm', 'Synthetic direct', 0, 'observed', true);
    EXCEPTION WHEN check_violation THEN
        rejected := true;
    END;
    IF NOT rejected THEN
        RAISE EXCEPTION 'Series without review references was approved';
    END IF;

    INSERT INTO measurement_series (
        id, station_id, source_id, external_id, variable_code, unit_id,
        unit, procedure_name, support_seconds, data_kind, approved,
        approval_version, rights_decision_id, hydrology_decision_id)
    VALUES (v_series_id, v_station_id, v_source_id, '31', 'H', 3,
            'm', 'Synthetic direct', 0, 'observed', true,
            'synthetic-v1', 'synthetic-rights', 'synthetic-hydrology');

    INSERT INTO measurements (
        series_id, observed_start_at, observed_end_at, value, quality,
        ingested_at, source_record_id, payload_hash)
    VALUES (v_series_id, '2026-10-01 03:00:00+00', '2026-10-01 03:00:00+00',
            0, 'accepted', '2026-10-01 04:00:00+00', '29286744105', repeat('a', 64))
    RETURNING id INTO v_measurement_id;
    INSERT INTO series_latest (series_id, measurement_id, calculated_at)
    VALUES (v_series_id, v_measurement_id, '2026-10-01 04:00:00+00');
    IF EXISTS (SELECT 1 FROM publishable_measurements WHERE id = v_measurement_id) THEN
        RAISE EXCEPTION 'Pending source leaked through publishable view';
    END IF;
    UPDATE data_sources
    SET permission_status = 'approved', rights_decision_id = 'synthetic-rights',
        reviewed_at = '2026-10-01 00:00:00+00'
    WHERE id = v_source_id;
    IF NOT EXISTS (SELECT 1 FROM publishable_measurements WHERE id = v_measurement_id) THEN
        RAISE EXCEPTION 'Reviewed source is missing from publishable view';
    END IF;
    UPDATE data_sources SET permission_status = 'denied' WHERE id = v_source_id;
    IF EXISTS (SELECT 1 FROM publishable_measurements WHERE id = v_measurement_id) THEN
        RAISE EXCEPTION 'Revoked source leaked through publishable view';
    END IF;

    rejected := false;
    BEGIN
        INSERT INTO measurements (
            series_id, observed_start_at, observed_end_at, value, quality,
            ingested_at, source_record_id, payload_hash)
        VALUES (v_series_id, '2026-10-01 03:00:00+00', '2026-10-01 03:00:00+00',
                1, 'accepted', '2026-10-01 05:00:00+00', 'different-id', repeat('b', 64));
    EXCEPTION WHEN unique_violation THEN
        rejected := true;
    END;
    IF NOT rejected THEN
        RAISE EXCEPTION 'Duplicate observation interval was inserted';
    END IF;
END;
$$;

ROLLBACK;
