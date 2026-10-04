BEGIN;

DO $$
DECLARE
    v_source uuid := gen_random_uuid();
    v_station uuid := gen_random_uuid();
    v_series uuid := gen_random_uuid();
    v_installation uuid := gen_random_uuid();
    v_rule uuid := gen_random_uuid();
    v_episode uuid := gen_random_uuid();
    v_event uuid := gen_random_uuid();
    v_claim uuid;
    v_expired_count integer;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM schema_migrations
                   WHERE version = '0008_notification_outbox') THEN
        RAISE EXCEPTION 'Notification migration missing';
    END IF;
    INSERT INTO data_sources
        (id, code, name, permission_status, rights_decision_id, reviewed_at)
    VALUES (v_source, 'outbox-smoke-' || v_source::text, 'Synthetic outbox source',
        'approved', 'synthetic-rights', now());
    INSERT INTO stations (id, name) VALUES (v_station, 'Synthetic outbox station');
    INSERT INTO measurement_series
        (id, station_id, source_id, external_id, variable_code, unit_id,
         unit, procedure_name, support_seconds, data_kind, approval_version,
         rights_decision_id, hydrology_decision_id, approved)
    VALUES (v_series, v_station, v_source, 'outbox-smoke', 'H', 1,
        'm', 'synthetic', 0, 'observed', 'synthetic-v1',
        'synthetic-rights', 'synthetic-hydrology', true);
    INSERT INTO notification_installations
        (id, credential_hash, platform, consent_version)
    VALUES (v_installation, decode(repeat('ab', 32), 'hex'), 'android', 'test-v1');
    INSERT INTO notification_rules
        (id, installation_id, series_id, trigger, maximum_age_seconds,
         delivery_ttl_seconds)
    VALUES (v_rule, v_installation, v_series, 'aboveAlertThreshold', 3600, 900);
    INSERT INTO notification_episodes
        (id, rule_id, rule_version, opened_at, cause_observed_at, expires_at)
    VALUES (v_episode, v_rule, 1, now(), now() - interval '1 minute',
        now() + interval '15 minutes');
    INSERT INTO notification_outbox (id, episode_id, expires_at)
    VALUES (v_event, v_episode, now() + interval '15 minutes');

    SELECT event_id INTO v_claim FROM claim_notification_outbox(
        'worker-a', interval '1 minute', 1);
    IF v_claim IS DISTINCT FROM v_event THEN
        RAISE EXCEPTION 'Pending event was not claimed';
    END IF;
    IF EXISTS (SELECT 1 FROM claim_notification_outbox(
        'worker-b', interval '1 minute', 1)) THEN
        RAISE EXCEPTION 'Live lease was claimed twice';
    END IF;
    IF accept_notification_outbox(v_event, 'worker-b') THEN
        RAISE EXCEPTION 'Wrong owner accepted a delivery';
    END IF;
    IF NOT accept_notification_outbox(v_event, 'worker-a') THEN
        RAISE EXCEPTION 'Owner could not accept delivery';
    END IF;
    IF accept_notification_outbox(v_event, 'worker-a') THEN
        RAISE EXCEPTION 'Delivery was accepted twice';
    END IF;

    UPDATE notification_episodes SET closed_at = clock_timestamp()
    WHERE id = v_episode;
    v_episode := gen_random_uuid();
    v_event := gen_random_uuid();
    INSERT INTO notification_episodes
        (id, rule_id, rule_version, opened_at, cause_observed_at, expires_at)
    VALUES (v_episode, v_rule, 1, now(), now(), now() + interval '15 minutes');
    INSERT INTO notification_outbox (id, episode_id, expires_at)
    VALUES (v_event, v_episode, now() + interval '15 minutes');
    UPDATE notification_installations SET revoked_at = clock_timestamp()
    WHERE id = v_installation;
    IF EXISTS (SELECT 1 FROM claim_notification_outbox(
        'worker-a', interval '1 minute', 1)) THEN
        RAISE EXCEPTION 'Revoked installation was still dispatchable';
    END IF;

    UPDATE notification_episodes SET closed_at = clock_timestamp()
    WHERE id = v_episode;
    v_episode := gen_random_uuid();
    v_event := gen_random_uuid();
    INSERT INTO notification_episodes
        (id, rule_id, rule_version, opened_at, cause_observed_at, expires_at)
    VALUES (v_episode, v_rule, 1, now() - interval '2 hours',
        now() - interval '2 hours', now() - interval '1 hour');
    INSERT INTO notification_outbox
        (id, episode_id, created_at, expires_at)
    VALUES (v_event, v_episode,
        now() - interval '2 hours', now() - interval '1 hour');
    v_expired_count := expire_notification_outbox();
    IF v_expired_count < 1 OR
       NOT EXISTS (SELECT 1 FROM notification_outbox
                   WHERE id = v_event AND state = 'expired') THEN
        RAISE EXCEPTION 'Expired delivery remained pending';
    END IF;
END;
$$;

ROLLBACK;
