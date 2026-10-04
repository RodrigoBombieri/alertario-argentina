BEGIN;

SELECT pg_advisory_xact_lock(72651001);

-- Events created before the causal latest version was recorded cannot be
-- revalidated safely. Cancel delivery and release the one-open-episode slot.
UPDATE notification_outbox AS o
SET state = 'cancelled', lease_owner = NULL, lease_until = NULL
FROM notification_episodes AS e
WHERE o.episode_id = e.id AND e.cause_latest_version IS NULL AND
      o.state IN ('pending', 'leased');

UPDATE notification_episodes
SET closed_at = greatest(opened_at, clock_timestamp())
WHERE closed_at IS NULL AND cause_latest_version IS NULL;

INSERT INTO schema_migrations(version) VALUES ('0010_cancel_unversioned_episodes');
COMMIT;
