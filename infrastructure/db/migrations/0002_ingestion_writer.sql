BEGIN;

SELECT pg_advisory_xact_lock(72651001);

CREATE OR REPLACE FUNCTION claim_ingestion_lease(
    p_provider text, p_stream_key text, p_owner text, p_duration interval)
RETURNS boolean LANGUAGE plpgsql AS $$
BEGIN
    IF nullif(trim(p_provider), '') IS NULL OR
       nullif(trim(p_stream_key), '') IS NULL OR
       nullif(trim(p_owner), '') IS NULL OR
       p_duration IS NULL OR p_duration <= interval '0 seconds' OR
       p_duration > interval '1 hour' THEN
        RAISE EXCEPTION 'Invalid lease parameters';
    END IF;
    INSERT INTO ingestion_checkpoints (provider, stream_key, lease_owner, lease_until)
    VALUES (p_provider, p_stream_key, p_owner, clock_timestamp() + p_duration)
    ON CONFLICT (provider, stream_key) DO UPDATE
    SET lease_owner = EXCLUDED.lease_owner,
        lease_until = EXCLUDED.lease_until
    WHERE ingestion_checkpoints.lease_until IS NULL OR
          ingestion_checkpoints.lease_until < clock_timestamp() OR
          ingestion_checkpoints.lease_owner = p_owner;
    RETURN FOUND;
END;
$$;

-- One call is one database transaction. The checkpoint cannot advance without the batch.
CREATE OR REPLACE FUNCTION commit_ingestion_batch(
    p_provider text, p_stream_key text, p_owner text, p_cursor text,
    p_coverage text, p_transport_at timestamptz, p_records jsonb)
RETURNS TABLE(inserted integer, unchanged integer, revised integer, quarantined integer)
LANGUAGE plpgsql AS $$
DECLARE
    v_checkpoint ingestion_checkpoints%ROWTYPE;
    v_record jsonb;
    v_series_id uuid;
    v_start timestamptz;
    v_end timestamptz;
    v_value numeric(14,5);
    v_source_updated timestamptz;
    v_hash text;
    v_record_id text;
    v_existing measurements%ROWTYPE;
    v_measurement_id bigint;
    v_reason text;
    v_received_at timestamptz := clock_timestamp();
    v_newest timestamptz;
    v_valid boolean := false;
    v_revised_series uuid[] := '{}';
BEGIN
    IF p_coverage NOT IN ('complete', 'partial', 'unavailable') OR
       p_transport_at IS NULL OR jsonb_typeof(p_records) <> 'array' THEN
        RAISE EXCEPTION 'Invalid batch envelope';
    END IF;
    SELECT * INTO v_checkpoint FROM ingestion_checkpoints
    WHERE provider = p_provider AND stream_key = p_stream_key FOR UPDATE;
    IF NOT FOUND OR v_checkpoint.lease_owner IS DISTINCT FROM p_owner OR
       v_checkpoint.lease_until <= clock_timestamp() THEN
        RAISE EXCEPTION 'Ingestion lease is absent or expired';
    END IF;

    inserted := 0; unchanged := 0; revised := 0; quarantined := 0;
    -- Lock all affected series in a stable order, including across streams.
    FOR v_series_id IN
        SELECT DISTINCT (value ->> 'seriesId')::uuid
        FROM jsonb_array_elements(p_records) ORDER BY 1
    LOOP
        PERFORM pg_advisory_xact_lock(hashtextextended(v_series_id::text, 72651002));
    END LOOP;

    FOR v_record IN SELECT value FROM jsonb_array_elements(p_records) LOOP
        IF jsonb_typeof(v_record) <> 'object' OR
           NOT (v_record ? 'seriesId' AND v_record ? 'observedStartAt' AND
                v_record ? 'observedEndAt' AND v_record ? 'payloadHash' AND
                v_record ? 'value') THEN
            RAISE EXCEPTION 'Malformed measurement record';
        END IF;
        v_series_id := (v_record ->> 'seriesId')::uuid;
        v_start := (v_record ->> 'observedStartAt')::timestamptz;
        v_end := (v_record ->> 'observedEndAt')::timestamptz;
        v_value := (v_record ->> 'value')::numeric(14,5);
        v_source_updated := (v_record ->> 'sourceUpdatedAt')::timestamptz;
        v_hash := v_record ->> 'payloadHash';
        v_record_id := nullif(v_record ->> 'sourceRecordId', '');
        IF v_series_id IS NULL OR v_start IS NULL OR v_end IS NULL OR
           v_end < v_start OR v_start > v_received_at OR
           v_hash !~ '^[0-9a-f]{64}$' OR v_hash IS NULL THEN
            RAISE EXCEPTION 'Invalid measurement identity or hash';
        END IF;

        SELECT * INTO v_existing FROM measurements
        WHERE series_id = v_series_id AND observed_start_at = v_start AND
              observed_end_at = v_end FOR UPDATE;
        v_reason := NULL;
        IF FOUND THEN
            IF v_record_id IS NOT NULL AND v_existing.source_record_id IS NOT NULL AND
               v_record_id <> v_existing.source_record_id THEN
                v_reason := 'sourceRecordConflict';
            ELSIF v_existing.payload_hash = v_hash THEN
                IF v_existing.value IS NOT DISTINCT FROM v_value THEN
                    unchanged := unchanged + 1;
                    v_valid := true;
                    CONTINUE;
                END IF;
                v_reason := 'hashValueConflict';
            ELSIF v_source_updated IS NULL OR
                  (v_existing.source_updated_at IS NOT NULL AND
                   v_source_updated <= v_existing.source_updated_at) THEN
                v_reason := 'unorderedCorrection';
            ELSIF v_existing.revision = 2147483647 THEN
                v_reason := 'revisionOverflow';
            ELSE
                INSERT INTO measurement_revisions (
                    measurement_id, revision, previous_value, previous_quality,
                    previous_source_updated_at, previous_payload_hash, received_at, reason)
                VALUES (v_existing.id, v_existing.revision + 1,
                    v_existing.value, v_existing.quality, v_existing.source_updated_at,
                    v_existing.payload_hash, v_received_at, 'sourceCorrection');
                UPDATE measurements SET value = v_value,
                    quality = CASE WHEN v_value IS NULL THEN 'missing' ELSE 'accepted' END,
                    source_updated_at = v_source_updated, ingested_at = v_received_at,
                    payload_hash = v_hash, revision = revision + 1
                WHERE id = v_existing.id;
                revised := revised + 1;
                v_valid := true;
                v_revised_series := array_append(v_revised_series, v_series_id);
            END IF;
        ELSE
            IF v_record_id IS NOT NULL AND EXISTS (
                SELECT 1 FROM measurements WHERE series_id = v_series_id AND
                    source_record_id = v_record_id) THEN
                v_reason := 'sourceRecordConflict';
            ELSE
                INSERT INTO measurements (
                    series_id, observed_start_at, observed_end_at, value, quality,
                    source_updated_at, ingested_at, source_record_id, payload_hash)
                VALUES (v_series_id, v_start, v_end, v_value,
                    CASE WHEN v_value IS NULL THEN 'missing' ELSE 'accepted' END,
                    v_source_updated, v_received_at, v_record_id, v_hash)
                RETURNING id INTO v_measurement_id;
                inserted := inserted + 1;
                v_valid := true;
                v_newest := greatest(v_newest, v_end);
            END IF;
        END IF;
        IF v_reason IS NOT NULL THEN
            INSERT INTO quarantined_records (
                provider, stream_key, source_record_id, reason, payload_hash, received_at)
            VALUES (p_provider, p_stream_key, v_record_id, v_reason, v_hash, v_received_at);
            quarantined := quarantined + 1;
        END IF;
    END LOOP;

    -- Rebuild latest for every touched series; a correction can remove its accepted value.
    INSERT INTO series_latest (series_id, measurement_id, calculated_at)
    SELECT ids.series_id, latest.id, v_received_at
    FROM (SELECT DISTINCT (value ->> 'seriesId')::uuid AS series_id
          FROM jsonb_array_elements(p_records)) AS ids
    CROSS JOIN LATERAL (
        SELECT id FROM measurements WHERE series_id = ids.series_id AND quality = 'accepted'
        ORDER BY observed_end_at DESC, observed_start_at DESC, id DESC LIMIT 1
    ) AS latest
    ON CONFLICT (series_id) DO UPDATE
    SET measurement_id = EXCLUDED.measurement_id,
        calculated_at = EXCLUDED.calculated_at,
        version = series_latest.version + 1
    WHERE series_latest.measurement_id <> EXCLUDED.measurement_id OR
          series_latest.series_id = ANY(v_revised_series);
    DELETE FROM series_latest AS l
    WHERE l.series_id IN (
        SELECT DISTINCT (value ->> 'seriesId')::uuid FROM jsonb_array_elements(p_records))
      AND NOT EXISTS (SELECT 1 FROM measurements AS m
                      WHERE m.series_id = l.series_id AND m.quality = 'accepted');

    UPDATE ingestion_checkpoints SET cursor = p_cursor,
        last_transport_success_at = p_transport_at,
        last_valid_ingestion_at = CASE WHEN v_valid THEN v_received_at
                                      ELSE last_valid_ingestion_at END,
        last_new_observation_at = greatest(last_new_observation_at, v_newest),
        coverage = CASE WHEN quarantined > 0 THEN 'partial' ELSE p_coverage END,
        version = version + 1
    WHERE provider = p_provider AND stream_key = p_stream_key;
    RETURN NEXT;
END;
$$;

INSERT INTO schema_migrations(version) VALUES ('0002_ingestion_writer')
ON CONFLICT DO NOTHING;
COMMIT;
