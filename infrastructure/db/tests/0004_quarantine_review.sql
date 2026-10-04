BEGIN;

DO $$
DECLARE
    v_id bigint;
    v_rejected boolean := false;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM schema_migrations
                   WHERE version = '0007_quarantine_review') THEN
        RAISE EXCEPTION 'Quarantine review migration missing';
    END IF;
    INSERT INTO quarantined_records (
        provider, stream_key, source_record_id, reason,
        payload_hash, received_at)
    VALUES ('synthetic-review-smoke', 'smoke-stream', 'sample-1',
        'normalizer:invalidUnit', repeat('a', 64), clock_timestamp())
    RETURNING id INTO v_id;

    IF NOT EXISTS (SELECT 1 FROM quarantined_records
                   WHERE id = v_id AND review_status = 'open' AND
                         reviewed_at IS NULL) THEN
        RAISE EXCEPTION 'New quarantine row is not open';
    END IF;
    BEGIN
        PERFORM review_quarantined_record(
            v_id, 'published', 'operator-test', 'Wrong disposition');
    EXCEPTION WHEN raise_exception THEN v_rejected := true;
    END;
    IF NOT v_rejected THEN RAISE EXCEPTION 'Invalid review accepted'; END IF;

    IF NOT review_quarantined_record(
        v_id, 'dismissed', ' operator-test ', ' Fixture inspected; no series approved. ')
    THEN RAISE EXCEPTION 'Valid review was not recorded'; END IF;
    IF EXISTS (SELECT 1 FROM quarantined_records
               WHERE id = v_id AND (review_status <> 'dismissed' OR
                     reviewed_at IS NULL OR reviewed_by <> 'operator-test' OR
                     review_note <> 'Fixture inspected; no series approved.')) THEN
        RAISE EXCEPTION 'Review evidence was not normalized';
    END IF;
    IF review_quarantined_record(
        v_id, 'reprocessed', 'operator-test', 'Should not replace audit record')
    THEN RAISE EXCEPTION 'Second disposition overwrote audit record'; END IF;
END;
$$;

ROLLBACK;
