BEGIN;

DO $$
DECLARE
    v_source uuid := '00000000-0000-4000-8000-000000000101';
    v_station uuid := '00000000-0000-4000-8000-000000000102';
    v_series uuid := '00000000-0000-4000-8000-000000000103';
    v_record jsonb;
    v_result record;
    v_id bigint;
    v_failed boolean;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM schema_migrations
                   WHERE version = '0002_ingestion_writer') THEN
        RAISE EXCEPTION 'Ingestion migration missing';
    END IF;
    INSERT INTO data_sources (id, code, name)
    VALUES (v_source, 'synthetic-writer-smoke', 'Synthetic writer source');
    INSERT INTO stations (id, name) VALUES (v_station, 'Synthetic writer station');
    INSERT INTO measurement_series (id, station_id, source_id, external_id,
        variable_code, unit, procedure_name)
    VALUES (v_series, v_station, v_source, 'synthetic-series', 'H', 'm', 'test');

    INSERT INTO ingestion_checkpoints (provider, stream_key)
    VALUES ('synthetic', 'stream');

    IF NOT claim_ingestion_lease('synthetic', 'stream', 'worker-a', interval '1 minute') OR
       claim_ingestion_lease('synthetic', 'stream', 'worker-b', interval '1 minute') THEN
        RAISE EXCEPTION 'Lease contention failed';
    END IF;
    v_record := jsonb_build_object('seriesId', v_series,
        'observedStartAt', '2026-10-01T03:00:00Z',
        'observedEndAt', '2026-10-01T03:00:00Z', 'value', 7.32,
        'sourceUpdatedAt', '2026-10-01T04:00:00Z',
        'sourceRecordId', 'observation-1', 'payloadHash', repeat('a', 64));
    SELECT * INTO v_result FROM commit_ingestion_batch('synthetic', 'stream',
        'worker-a', 'cursor-1', 'complete', clock_timestamp(),
        jsonb_build_array(v_record));
    IF v_result.inserted <> 1 OR v_result.unchanged <> 0 OR
       (SELECT version FROM ingestion_checkpoints WHERE provider = 'synthetic'
        AND stream_key = 'stream') <> 1 THEN
        RAISE EXCEPTION 'First batch did not commit';
    END IF;
    SELECT id INTO v_id FROM measurements WHERE series_id = v_series;
    IF NOT EXISTS (SELECT 1 FROM series_latest WHERE series_id = v_series
                   AND measurement_id = v_id) OR
       EXISTS (SELECT 1 FROM publishable_measurements WHERE id = v_id) THEN
        RAISE EXCEPTION 'Latest or publication gate is incorrect';
    END IF;

    SELECT * INTO v_result FROM commit_ingestion_batch('synthetic', 'stream',
        'worker-a', 'cursor-2', 'complete', clock_timestamp(),
        jsonb_build_array(v_record));
    IF v_result.unchanged <> 1 OR
       (SELECT count(*) FROM measurements WHERE series_id = v_series) <> 1 OR
       (SELECT version FROM series_latest WHERE series_id = v_series) <> 1 THEN
        RAISE EXCEPTION 'Replay changed measurement or latest';
    END IF;

    v_record := jsonb_set(v_record, '{value}', '7.48'::jsonb);
    v_record := jsonb_set(v_record, '{sourceUpdatedAt}',
        '"2026-10-01T05:00:00Z"'::jsonb);
    v_record := jsonb_set(v_record, '{payloadHash}',
        to_jsonb(repeat('b', 64)));
    SELECT * INTO v_result FROM commit_ingestion_batch('synthetic', 'stream',
        'worker-a', 'cursor-3', 'complete', clock_timestamp(),
        jsonb_build_array(v_record));
    IF v_result.revised <> 1 OR
       (SELECT revision FROM measurements WHERE id = v_id) <> 2 OR
       (SELECT previous_value FROM measurement_revisions
        WHERE measurement_id = v_id AND revision = 2) <> 7.32 OR
       (SELECT version FROM series_latest WHERE series_id = v_series) <> 2 THEN
        RAISE EXCEPTION 'Correction history or latest version is incorrect';
    END IF;

    v_record := jsonb_set(v_record, '{value}', '8.00'::jsonb);
    v_record := jsonb_set(v_record, '{sourceUpdatedAt}',
        '"2026-10-01T04:00:00Z"'::jsonb);
    v_record := jsonb_set(v_record, '{payloadHash}',
        to_jsonb(repeat('c', 64)));
    SELECT * INTO v_result FROM commit_ingestion_batch('synthetic', 'stream',
        'worker-a', 'cursor-4', 'complete', clock_timestamp(),
        jsonb_build_array(v_record));
    IF v_result.quarantined <> 1 OR
       (SELECT value FROM measurements WHERE id = v_id) <> 7.48 OR
       (SELECT coverage FROM ingestion_checkpoints WHERE provider = 'synthetic'
        AND stream_key = 'stream') <> 'partial' THEN
        RAISE EXCEPTION 'Unordered correction was not quarantined';
    END IF;

    v_failed := false;
    BEGIN
        PERFORM commit_ingestion_batch('synthetic', 'stream', 'worker-b',
            'forbidden', 'complete', clock_timestamp(), '[]'::jsonb);
    EXCEPTION WHEN raise_exception THEN v_failed := true;
    END;
    IF NOT v_failed THEN RAISE EXCEPTION 'Non-owner advanced checkpoint'; END IF;

    v_failed := false;
    BEGIN
        PERFORM commit_ingestion_batch('synthetic', 'stream', 'worker-a',
            'bad-cursor', 'complete', clock_timestamp(),
            jsonb_build_array(v_record, jsonb_build_object('value', 2)));
    EXCEPTION WHEN raise_exception THEN v_failed := true;
    END;
    IF NOT v_failed OR
       (SELECT cursor FROM ingestion_checkpoints WHERE provider = 'synthetic'
        AND stream_key = 'stream') <> 'cursor-4' OR
       (SELECT count(*) FROM quarantined_records WHERE provider = 'synthetic') <> 1 THEN
        RAISE EXCEPTION 'Failed batch advanced cursor or persisted partial writes';
    END IF;
END;
$$;

ROLLBACK;
