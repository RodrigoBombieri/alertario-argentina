BEGIN;

SELECT pg_advisory_xact_lock(72651001);

CREATE TABLE notification_installations (
    id uuid PRIMARY KEY,
    credential_hash bytea NOT NULL CHECK (octet_length(credential_hash) = 32),
    platform text NOT NULL CHECK (platform IN ('android', 'ios')),
    consent_version text NOT NULL CHECK (length(trim(consent_version)) BETWEEN 1 AND 100),
    created_at timestamptz NOT NULL DEFAULT now(),
    revoked_at timestamptz
);

CREATE TABLE notification_rules (
    id uuid PRIMARY KEY,
    installation_id uuid NOT NULL REFERENCES notification_installations(id),
    series_id uuid NOT NULL REFERENCES measurement_series(id),
    trigger text NOT NULL CHECK (trigger IN (
        'followUp', 'aboveAlertThreshold', 'aboveEvacuationThreshold')),
    maximum_age_seconds integer NOT NULL CHECK (maximum_age_seconds BETWEEN 1 AND 172800),
    delivery_ttl_seconds integer NOT NULL CHECK (delivery_ttl_seconds BETWEEN 1 AND 86400),
    enabled boolean NOT NULL DEFAULT true,
    version integer NOT NULL DEFAULT 1 CHECK (version > 0),
    created_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX notification_rules_series_enabled_idx
    ON notification_rules (series_id) WHERE enabled;

CREATE TABLE notification_episodes (
    id uuid PRIMARY KEY,
    rule_id uuid NOT NULL REFERENCES notification_rules(id),
    rule_version integer NOT NULL CHECK (rule_version > 0),
    opened_at timestamptz NOT NULL,
    cause_observed_at timestamptz NOT NULL,
    expires_at timestamptz NOT NULL,
    closed_at timestamptz,
    CHECK (expires_at > opened_at),
    CHECK (closed_at IS NULL OR closed_at >= opened_at)
);
CREATE UNIQUE INDEX notification_episodes_one_open_idx
    ON notification_episodes (rule_id) WHERE closed_at IS NULL;

CREATE TABLE notification_outbox (
    id uuid PRIMARY KEY,
    episode_id uuid NOT NULL UNIQUE REFERENCES notification_episodes(id),
    state text NOT NULL DEFAULT 'pending' CHECK (state IN (
        'pending', 'leased', 'accepted', 'cancelled', 'expired')),
    created_at timestamptz NOT NULL DEFAULT now(),
    expires_at timestamptz NOT NULL,
    next_attempt_at timestamptz NOT NULL DEFAULT now(),
    attempts integer NOT NULL DEFAULT 0 CHECK (attempts >= 0),
    lease_owner text,
    lease_until timestamptz,
    accepted_at timestamptz,
    CHECK (expires_at > created_at),
    CHECK ((state = 'leased') = (lease_owner IS NOT NULL AND lease_until IS NOT NULL)),
    CHECK ((state = 'accepted') = (accepted_at IS NOT NULL))
);
CREATE INDEX notification_outbox_due_idx
    ON notification_outbox (next_attempt_at, created_at)
    WHERE state IN ('pending', 'leased');

CREATE FUNCTION claim_notification_outbox(
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
        WHERE candidate.expires_at > clock_timestamp() AND
              e.closed_at IS NULL AND r.enabled AND i.revoked_at IS NULL AND
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

CREATE FUNCTION accept_notification_outbox(p_id uuid, p_owner text)
RETURNS boolean LANGUAGE plpgsql AS $$
BEGIN
    UPDATE notification_outbox AS o
    SET state = 'accepted', accepted_at = clock_timestamp(),
        lease_owner = NULL, lease_until = NULL
    FROM notification_episodes AS e
    JOIN notification_rules AS r ON r.id = e.rule_id
    JOIN notification_installations AS i ON i.id = r.installation_id
    WHERE o.id = p_id AND o.episode_id = e.id AND o.state = 'leased' AND
          o.lease_owner = p_owner AND o.lease_until > clock_timestamp() AND
          o.expires_at > clock_timestamp() AND e.closed_at IS NULL AND
          r.enabled AND i.revoked_at IS NULL;
    RETURN FOUND;
END;
$$;

CREATE FUNCTION retry_notification_outbox(
    p_id uuid, p_owner text, p_next_attempt_at timestamptz)
RETURNS boolean LANGUAGE plpgsql AS $$
BEGIN
    UPDATE notification_outbox
    SET state = 'pending', next_attempt_at = p_next_attempt_at,
        lease_owner = NULL, lease_until = NULL
    WHERE id = p_id AND state = 'leased' AND lease_owner = p_owner AND
          lease_until > clock_timestamp() AND expires_at > p_next_attempt_at AND
          p_next_attempt_at > clock_timestamp();
    RETURN FOUND;
END;
$$;

CREATE FUNCTION expire_notification_outbox()
RETURNS integer LANGUAGE plpgsql AS $$
DECLARE v_count integer;
BEGIN
    UPDATE notification_outbox
    SET state = 'expired', lease_owner = NULL, lease_until = NULL
    WHERE state IN ('pending', 'leased') AND expires_at <= clock_timestamp();
    GET DIAGNOSTICS v_count = ROW_COUNT;
    RETURN v_count;
END;
$$;

INSERT INTO schema_migrations(version) VALUES ('0008_notification_outbox');
COMMIT;
