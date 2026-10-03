BEGIN;

DO $$
DECLARE
    v_source uuid := '00000000-0000-4000-8000-000000000301';
    v_station uuid := '00000000-0000-4000-8000-000000000302';
    v_series uuid := '00000000-0000-4000-8000-000000000303';
    v_rejected boolean;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM schema_migrations
                   WHERE version = '0003_trend_policy') THEN
        RAISE EXCEPTION 'Trend policy migration missing';
    END IF;
    INSERT INTO data_sources (id, code, name)
    VALUES (v_source, 'synthetic-policy-smoke', 'Synthetic policy source');
    INSERT INTO stations (id, name) VALUES (v_station, 'Synthetic policy station');
    INSERT INTO measurement_series (id, station_id, source_id, external_id,
        variable_code, unit, procedure_name)
    VALUES (v_series, v_station, v_source, 'policy-series', 'H', 'm', 'test');

    v_rejected := false;
    BEGIN
        INSERT INTO series_trend_policies (series_id, version, cadence_seconds,
            allowed_lag_seconds, epsilon, methodology_version, datum_ref,
            epoch, active)
        VALUES (v_series, 'v1', 3600, 1800, 0.02, 'trend-v1', 'datum-v1', 1, true);
    EXCEPTION WHEN check_violation THEN v_rejected := true;
    END;
    IF NOT v_rejected THEN RAISE EXCEPTION 'Unreviewed policy activated'; END IF;

    INSERT INTO series_trend_policies (series_id, version, cadence_seconds,
        allowed_lag_seconds, epsilon, methodology_version, datum_ref,
        epoch, active, hydrology_decision_id, reviewed_at)
    VALUES (v_series, 'v1', 3600, 1800, 0.02, 'trend-v1', 'datum-v1', 1,
        true, 'synthetic-hydrology', now());
    v_rejected := false;
    BEGIN
        INSERT INTO series_trend_policies (series_id, version, cadence_seconds,
            allowed_lag_seconds, epsilon, methodology_version, datum_ref,
            epoch, active, hydrology_decision_id, reviewed_at)
        VALUES (v_series, 'v2', 3600, 1800, 0.03, 'trend-v2', 'datum-v1', 1,
            true, 'synthetic-hydrology', now());
    EXCEPTION WHEN unique_violation THEN v_rejected := true;
    END;
    IF NOT v_rejected THEN RAISE EXCEPTION 'Two active policies accepted'; END IF;

    v_rejected := false;
    BEGIN
        INSERT INTO official_thresholds (id, series_id, version, kind, value,
            unit, datum_ref, epoch, authority, title, source_url, valid_from,
            approved)
        VALUES ('00000000-0000-4000-8000-000000000304', v_series, 'v1',
            'alert', 11, 'm', 'datum-v1', 1, 'Synthetic authority',
            'Synthetic threshold', 'https://example.invalid/', now(), true);
    EXCEPTION WHEN check_violation THEN v_rejected := true;
    END;
    IF NOT v_rejected THEN RAISE EXCEPTION 'Unreviewed threshold approved'; END IF;
END;
$$;

ROLLBACK;
