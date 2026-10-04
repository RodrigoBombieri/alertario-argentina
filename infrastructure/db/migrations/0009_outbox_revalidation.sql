BEGIN;

SELECT pg_advisory_xact_lock(72651001);

-- Existing pending events have no linked latest version and remain non-dispatchable.
ALTER TABLE notification_episodes
    ADD COLUMN cause_latest_version bigint CHECK (cause_latest_version > 0);

CREATE OR REPLACE FUNCTION claim_notification_outbox(
    p_owner text, p_duration interval, p_limit integer)
RETURNS TABLE(event_id uuid, episode_id uuid, expires_at timestamptz)
LANGUAGE plpgsql AS $$
BEGIN
    IF coalesce(length(trim(p_owner)), 0) NOT BETWEEN 1 AND 200 OR
       p_duration IS NULL OR p_duration <= interval '0 seconds' OR
       p_duration > interval '15 minutes' OR p_limit IS NULL OR
       p_limit NOT BETWEEN 1 AND 100 THEN
        RAISE EXCEPTION 'Invalid outbox claim';
    END IF;
    RETURN QUERY
    UPDATE notification_outbox AS o
    SET state = 'leased', lease_owner = trim(p_owner),
        lease_until = clock_timestamp() + p_duration, attempts = attempts + 1
    WHERE o.id IN (
        SELECT candidate.id FROM notification_outbox AS candidate
        JOIN notification_episodes AS e ON e.id = candidate.episode_id
        JOIN notification_rules AS r ON r.id = e.rule_id
        JOIN notification_installations AS i ON i.id = r.installation_id
        JOIN series_latest AS l ON l.series_id = r.series_id
        JOIN publishable_measurements AS p
          ON p.id = l.measurement_id AND p.series_id = l.series_id
        WHERE candidate.expires_at > clock_timestamp() AND
              e.expires_at > clock_timestamp() AND e.closed_at IS NULL AND
              e.rule_version = r.version AND
              e.cause_latest_version = l.version AND
              e.cause_observed_at = p.observed_end_at AND
              r.enabled AND i.revoked_at IS NULL AND
              ((candidate.state = 'pending' AND
                candidate.next_attempt_at <= clock_timestamp()) OR
               (candidate.state = 'leased' AND
                candidate.lease_until < clock_timestamp()))
        ORDER BY candidate.created_at, candidate.id
        FOR UPDATE OF candidate SKIP LOCKED LIMIT p_limit
    )
    RETURNING o.id, o.episode_id, o.expires_at;
END;
$$;

CREATE OR REPLACE FUNCTION accept_notification_outbox(p_id uuid, p_owner text)
RETURNS boolean LANGUAGE plpgsql AS $$
BEGIN
    UPDATE notification_outbox AS o
    SET state = 'accepted', accepted_at = clock_timestamp(),
        lease_owner = NULL, lease_until = NULL
    FROM notification_episodes AS e
    JOIN notification_rules AS r ON r.id = e.rule_id
    JOIN notification_installations AS i ON i.id = r.installation_id
    JOIN series_latest AS l ON l.series_id = r.series_id
    JOIN publishable_measurements AS p
      ON p.id = l.measurement_id AND p.series_id = l.series_id
    WHERE o.id = p_id AND o.episode_id = e.id AND o.state = 'leased' AND
          o.lease_owner = p_owner AND o.lease_until > clock_timestamp() AND
          o.expires_at > clock_timestamp() AND e.expires_at > clock_timestamp() AND
          e.closed_at IS NULL AND e.rule_version = r.version AND
          e.cause_latest_version = l.version AND
          e.cause_observed_at = p.observed_end_at AND
          r.enabled AND i.revoked_at IS NULL;
    RETURN FOUND;
END;
$$;

INSERT INTO schema_migrations(version) VALUES ('0009_outbox_revalidation');
COMMIT;
