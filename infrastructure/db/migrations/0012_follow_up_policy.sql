BEGIN;
SELECT pg_advisory_xact_lock(72651001);

ALTER TABLE series_trend_policies
    ADD COLUMN follow_up_window_hours integer,
    ADD COLUMN follow_up_minimum_rise numeric(14,5),
    ADD CONSTRAINT follow_up_complete CHECK (
        (follow_up_window_hours IS NULL AND follow_up_minimum_rise IS NULL) OR
        (follow_up_window_hours IS NOT NULL AND follow_up_minimum_rise IS NOT NULL
         AND follow_up_window_hours IN (1, 3, 6, 12, 24) AND follow_up_minimum_rise > 0));

-- Optional, versioned together with the hydrologist-approved policy. Never seed real values.
INSERT INTO schema_migrations(version) VALUES ('0012_follow_up_policy');
COMMIT;
