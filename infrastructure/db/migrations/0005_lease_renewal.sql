BEGIN;

SELECT pg_advisory_xact_lock(72651001);

-- A worker may extend only its own unexpired lease. Once expired it must
-- claim again and re-read the checkpoint before processing more data.
CREATE FUNCTION renew_ingestion_lease(
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
    UPDATE ingestion_checkpoints
    SET lease_until = clock_timestamp() + p_duration
    WHERE provider = p_provider AND stream_key = p_stream_key AND
          lease_owner = p_owner AND lease_until > clock_timestamp();
    RETURN FOUND;
END;
$$;

INSERT INTO schema_migrations(version) VALUES ('0005_lease_renewal');
COMMIT;
