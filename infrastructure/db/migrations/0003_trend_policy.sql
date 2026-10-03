BEGIN;

SELECT pg_advisory_xact_lock(72651001);

CREATE TABLE series_trend_policies (
    series_id uuid NOT NULL REFERENCES measurement_series(id),
    version text NOT NULL,
    cadence_seconds integer NOT NULL CHECK (cadence_seconds > 0),
    allowed_lag_seconds integer NOT NULL CHECK (allowed_lag_seconds >= 0),
    epsilon numeric(14,5) NOT NULL CHECK (epsilon >= 0),
    methodology_version text NOT NULL,
    datum_ref text NOT NULL,
    epoch integer NOT NULL CHECK (epoch > 0),
    active boolean NOT NULL DEFAULT false,
    hydrology_decision_id text,
    reviewed_at timestamptz,
    PRIMARY KEY (series_id, version),
    CHECK (nullif(trim(version), '') IS NOT NULL AND
           nullif(trim(methodology_version), '') IS NOT NULL AND
           nullif(trim(datum_ref), '') IS NOT NULL),
    CHECK (active = false OR
           (nullif(trim(hydrology_decision_id), '') IS NOT NULL AND
            reviewed_at IS NOT NULL))
);
CREATE UNIQUE INDEX series_trend_policies_one_active_uq
    ON series_trend_policies (series_id) WHERE active;

CREATE TABLE official_thresholds (
    id uuid PRIMARY KEY,
    series_id uuid NOT NULL REFERENCES measurement_series(id),
    version text NOT NULL,
    kind text NOT NULL CHECK (kind IN ('alert', 'evacuation_reference')),
    value numeric(14,5) NOT NULL,
    unit text NOT NULL,
    datum_ref text NOT NULL,
    epoch integer NOT NULL CHECK (epoch > 0),
    authority text NOT NULL,
    title text NOT NULL,
    source_url text NOT NULL,
    valid_from timestamptz NOT NULL,
    valid_until timestamptz,
    approved boolean NOT NULL DEFAULT false,
    rights_decision_id text,
    hydrology_decision_id text,
    reviewed_at timestamptz,
    CHECK (valid_until IS NULL OR valid_until > valid_from),
    CHECK (nullif(trim(version), '') IS NOT NULL AND
           nullif(trim(unit), '') IS NOT NULL AND
           nullif(trim(datum_ref), '') IS NOT NULL),
    CHECK (approved = false OR
           (nullif(trim(authority), '') IS NOT NULL AND
            nullif(trim(title), '') IS NOT NULL AND
            nullif(trim(source_url), '') IS NOT NULL AND
            nullif(trim(rights_decision_id), '') IS NOT NULL AND
            nullif(trim(hydrology_decision_id), '') IS NOT NULL AND
            reviewed_at IS NOT NULL))
);
CREATE INDEX official_thresholds_series_validity_idx
    ON official_thresholds (series_id, valid_from, valid_until)
    WHERE approved;

INSERT INTO schema_migrations(version) VALUES ('0003_trend_policy');
COMMIT;
