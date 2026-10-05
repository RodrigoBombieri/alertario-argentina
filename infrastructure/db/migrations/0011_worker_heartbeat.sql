BEGIN;

SELECT pg_advisory_xact_lock(72651001);

CREATE TABLE worker_heartbeats (
    instance_id text PRIMARY KEY CHECK (length(instance_id) BETWEEN 1 AND 200),
    last_seen_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX worker_heartbeats_recent_idx ON worker_heartbeats (last_seen_at DESC);

INSERT INTO schema_migrations(version) VALUES ('0011_worker_heartbeat');
COMMIT;
