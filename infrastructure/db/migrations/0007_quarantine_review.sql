BEGIN;

SELECT pg_advisory_xact_lock(72651001);

ALTER TABLE quarantined_records
    ADD COLUMN review_status text NOT NULL DEFAULT 'open',
    ADD COLUMN reviewed_at timestamptz,
    ADD COLUMN reviewed_by text,
    ADD COLUMN review_note text,
    ADD CONSTRAINT quarantined_review_check CHECK (
        (review_status = 'open' AND reviewed_at IS NULL AND
         reviewed_by IS NULL AND review_note IS NULL) OR
        (review_status IN ('reprocessed', 'dismissed') AND
         reviewed_at IS NOT NULL AND
         coalesce(length(trim(reviewed_by)), 0) BETWEEN 1 AND 200 AND
         coalesce(length(trim(review_note)), 0) BETWEEN 1 AND 2000)
    );

CREATE INDEX quarantined_records_open_idx
    ON quarantined_records (provider, stream_key, received_at DESC)
    WHERE review_status = 'open';

-- Human review records a disposition; it never inserts a measurement or
-- changes checkpoint coverage. A replacement must use normal ingestion.
CREATE FUNCTION review_quarantined_record(
    p_id bigint, p_disposition text, p_reviewer text, p_note text)
RETURNS boolean LANGUAGE plpgsql AS $$
BEGIN
    IF p_id IS NULL OR p_id < 1 OR
       p_disposition IS NULL OR
       p_disposition NOT IN ('reprocessed', 'dismissed') OR
       coalesce(length(trim(p_reviewer)), 0) NOT BETWEEN 1 AND 200 OR
       coalesce(length(trim(p_note)), 0) NOT BETWEEN 1 AND 2000 THEN
        RAISE EXCEPTION 'Invalid quarantine review';
    END IF;
    UPDATE quarantined_records
    SET review_status = p_disposition,
        reviewed_at = clock_timestamp(),
        reviewed_by = trim(p_reviewer),
        review_note = trim(p_note)
    WHERE id = p_id AND review_status = 'open';
    RETURN FOUND;
END;
$$;

INSERT INTO schema_migrations(version) VALUES ('0007_quarantine_review');
COMMIT;
