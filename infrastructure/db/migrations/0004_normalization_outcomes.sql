BEGIN;

SELECT pg_advisory_xact_lock(72651001);

-- The nested function and rejected rows share the caller's transaction.
CREATE FUNCTION commit_ingestion_outcomes(
    p_provider text, p_stream_key text, p_owner text, p_cursor text,
    p_coverage text, p_transport_at timestamptz,
    p_records jsonb, p_rejected jsonb)
RETURNS TABLE(inserted integer, unchanged integer, revised integer, quarantined integer)
LANGUAGE plpgsql AS $$
DECLARE
    v_summary record;
    v_rejection jsonb;
    v_reason text;
    v_hash text;
    v_record_id text;
    v_rejected_count integer := 0;
BEGIN
    IF p_rejected IS NULL OR jsonb_typeof(p_rejected) <> 'array' OR
       jsonb_array_length(p_rejected) > 1000 THEN
        RAISE EXCEPTION 'Invalid rejected-record batch';
    END IF;
    SELECT * INTO v_summary FROM commit_ingestion_batch(
        p_provider, p_stream_key, p_owner, p_cursor, p_coverage,
        p_transport_at, p_records);

    FOR v_rejection IN SELECT value FROM jsonb_array_elements(p_rejected) LOOP
        IF jsonb_typeof(v_rejection) <> 'object' THEN
            RAISE EXCEPTION 'Malformed rejected record';
        END IF;
        v_reason := v_rejection ->> 'reason';
        v_hash := v_rejection ->> 'payloadHash';
        v_record_id := v_rejection ->> 'sourceRecordId';
        IF v_reason IS NULL OR v_reason !~ '^normalizer:[a-zA-Z]{2,80}$' OR
           v_hash IS NULL OR v_hash !~ '^[0-9a-f]{64}$' THEN
            RAISE EXCEPTION 'Invalid rejected-record evidence';
        END IF;
        IF NOT EXISTS (
            SELECT 1 FROM quarantined_records
            WHERE provider = p_provider AND stream_key = p_stream_key AND
                  payload_hash = v_hash AND reason = v_reason
        ) THEN
            INSERT INTO quarantined_records (
                provider, stream_key, source_record_id, reason,
                payload_hash, received_at)
            VALUES (p_provider, p_stream_key, v_record_id, v_reason,
                v_hash, clock_timestamp());
            v_rejected_count := v_rejected_count + 1;
        END IF;
    END LOOP;
    IF jsonb_array_length(p_rejected) > 0 THEN
        UPDATE ingestion_checkpoints SET coverage = 'partial'
        WHERE provider = p_provider AND stream_key = p_stream_key;
    END IF;
    inserted := v_summary.inserted;
    unchanged := v_summary.unchanged;
    revised := v_summary.revised;
    quarantined := v_summary.quarantined + v_rejected_count;
    RETURN NEXT;
END;
$$;

INSERT INTO schema_migrations(version) VALUES ('0004_normalization_outcomes');
COMMIT;
