BEGIN;

SELECT pg_advisory_xact_lock(72651001);
CREATE EXTENSION IF NOT EXISTS postgis;

CREATE TABLE schema_migrations (
    version text PRIMARY KEY,
    applied_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE data_sources (
    id uuid PRIMARY KEY,
    code text NOT NULL UNIQUE,
    name text NOT NULL,
    owner_name text,
    distribution_url text,
    license_url text,
    permission_status text NOT NULL DEFAULT 'pending'
        CHECK (permission_status IN ('pending', 'approved', 'denied')),
    rights_decision_id text,
    reviewed_at timestamptz,
    CHECK (permission_status <> 'approved' OR
           (nullif(trim(rights_decision_id), '') IS NOT NULL AND reviewed_at IS NOT NULL))
);

CREATE TABLE rivers (
    id uuid PRIMARY KEY,
    name text NOT NULL
);

CREATE TABLE stations (
    id uuid PRIMARY KEY,
    name text NOT NULL,
    river_id uuid REFERENCES rivers(id),
    point geography(Point, 4326),
    active boolean NOT NULL DEFAULT true,
    station_epoch integer NOT NULL DEFAULT 1 CHECK (station_epoch > 0)
);
CREATE INDEX stations_point_gix ON stations USING gist (point);

CREATE TABLE station_external_refs (
    station_id uuid NOT NULL REFERENCES stations(id),
    source_id uuid NOT NULL REFERENCES data_sources(id),
    network_key text NOT NULL,
    external_id text NOT NULL,
    external_code text,
    public_status boolean NOT NULL DEFAULT false,
    PRIMARY KEY (source_id, network_key, external_id)
);

CREATE TABLE measurement_series (
    id uuid PRIMARY KEY,
    station_id uuid NOT NULL REFERENCES stations(id),
    source_id uuid NOT NULL REFERENCES data_sources(id),
    external_id text NOT NULL,
    variable_code text NOT NULL,
    unit_id integer,
    unit text NOT NULL,
    procedure_id integer,
    procedure_name text NOT NULL,
    support_seconds bigint,
    data_kind text NOT NULL DEFAULT 'unknown'
        CHECK (data_kind IN ('unknown', 'observed', 'simulated', 'aggregated')),
    datum_ref text,
    epoch integer NOT NULL DEFAULT 1 CHECK (epoch > 0),
    cadence_seconds integer CHECK (cadence_seconds > 0),
    allowed_lag_seconds integer CHECK (allowed_lag_seconds >= 0),
    approval_version text,
    rights_decision_id text,
    hydrology_decision_id text,
    approved boolean NOT NULL DEFAULT false,
    UNIQUE (source_id, external_id, epoch),
    CHECK (approved = false OR
           (data_kind = 'observed' AND support_seconds IS NOT NULL AND
            support_seconds = 0 AND unit_id IS NOT NULL AND
            nullif(trim(approval_version), '') IS NOT NULL AND
            nullif(trim(rights_decision_id), '') IS NOT NULL AND
            nullif(trim(hydrology_decision_id), '') IS NOT NULL))
);
CREATE INDEX measurement_series_station_variable_idx
    ON measurement_series (station_id, variable_code);

CREATE TABLE catalog_snapshots (
    source_id uuid NOT NULL REFERENCES data_sources(id),
    version text NOT NULL,
    status text NOT NULL CHECK (status IN ('staging', 'complete')),
    row_count integer NOT NULL CHECK (row_count > 0),
    imported_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (source_id, version),
    UNIQUE (source_id, version, status)
);

CREATE TABLE locations (
    id uuid PRIMARY KEY,
    source_id uuid NOT NULL,
    external_id text NOT NULL,
    catalog_version text NOT NULL,
    name text NOT NULL,
    normalized_name text NOT NULL,
    category text NOT NULL,
    province_id text NOT NULL,
    province_name text NOT NULL,
    point geography(Point, 4326),
    UNIQUE (source_id, external_id, catalog_version),
    FOREIGN KEY (source_id, catalog_version)
        REFERENCES catalog_snapshots(source_id, version)
);
CREATE INDEX locations_name_province_idx
    ON locations (normalized_name, province_id);
CREATE INDEX locations_point_gix ON locations USING gist (point);

CREATE TABLE active_catalogs (
    source_id uuid PRIMARY KEY REFERENCES data_sources(id),
    catalog_version text NOT NULL,
    snapshot_status text NOT NULL DEFAULT 'complete'
        CHECK (snapshot_status = 'complete'),
    activated_at timestamptz NOT NULL DEFAULT now(),
    FOREIGN KEY (source_id, catalog_version, snapshot_status)
        REFERENCES catalog_snapshots(source_id, version, status)
);

CREATE FUNCTION activate_catalog_snapshot(p_source_id uuid, p_version text)
RETURNS void LANGUAGE plpgsql AS $$
DECLARE
    expected_rows integer;
    actual_rows bigint;
BEGIN
    SELECT row_count INTO expected_rows
    FROM catalog_snapshots
    WHERE source_id = p_source_id AND version = p_version
    FOR UPDATE;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'Catalog snapshot does not exist';
    END IF;
    SELECT count(*) INTO actual_rows
    FROM locations
    WHERE source_id = p_source_id AND catalog_version = p_version;
    IF actual_rows <> expected_rows THEN
        RAISE EXCEPTION 'Catalog snapshot is incomplete';
    END IF;
    UPDATE catalog_snapshots SET status = 'complete'
    WHERE source_id = p_source_id AND version = p_version;
    INSERT INTO active_catalogs (source_id, catalog_version)
    VALUES (p_source_id, p_version)
    ON CONFLICT (source_id) DO UPDATE
        SET catalog_version = EXCLUDED.catalog_version,
            activated_at = now();
END;
$$;

CREATE TABLE location_station_associations (
    id uuid PRIMARY KEY,
    location_id uuid NOT NULL REFERENCES locations(id),
    station_id uuid NOT NULL REFERENCES stations(id),
    valid_from timestamptz NOT NULL,
    valid_to timestamptz,
    status text NOT NULL CHECK (status IN ('pending', 'approved', 'rejected')),
    reason text NOT NULL,
    reviewed_by text,
    CHECK (valid_to IS NULL OR valid_to > valid_from),
    CHECK (status <> 'approved' OR nullif(trim(reviewed_by), '') IS NOT NULL)
);
CREATE UNIQUE INDEX location_station_active_approved_uq
    ON location_station_associations (location_id, station_id)
    WHERE valid_to IS NULL AND status = 'approved';

CREATE TABLE measurements (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    series_id uuid NOT NULL REFERENCES measurement_series(id),
    observed_start_at timestamptz NOT NULL,
    observed_end_at timestamptz NOT NULL,
    value numeric(14,5),
    quality text NOT NULL CHECK (quality IN ('accepted', 'missing')),
    source_updated_at timestamptz,
    ingested_at timestamptz NOT NULL,
    source_record_id text,
    payload_hash char(64) NOT NULL CHECK (payload_hash ~ '^[0-9a-f]{64}$'),
    revision integer NOT NULL DEFAULT 1 CHECK (revision > 0),
    CHECK (observed_end_at >= observed_start_at),
    CHECK ((quality = 'missing') = (value IS NULL)),
    UNIQUE (series_id, observed_start_at, observed_end_at),
    UNIQUE (id, series_id)
);
CREATE UNIQUE INDEX measurements_source_record_uq
    ON measurements (series_id, source_record_id)
    WHERE source_record_id IS NOT NULL;
CREATE INDEX measurements_series_start_idx
    ON measurements (series_id, observed_start_at DESC);

CREATE TABLE measurement_revisions (
    measurement_id bigint NOT NULL REFERENCES measurements(id),
    revision integer NOT NULL CHECK (revision > 1),
    previous_value numeric(14,5),
    previous_quality text NOT NULL CHECK (previous_quality IN ('accepted', 'missing')),
    previous_source_updated_at timestamptz,
    previous_payload_hash char(64) NOT NULL
        CHECK (previous_payload_hash ~ '^[0-9a-f]{64}$'),
    received_at timestamptz NOT NULL,
    reason text NOT NULL,
    PRIMARY KEY (measurement_id, revision),
    CHECK ((previous_quality = 'missing') = (previous_value IS NULL))
);

CREATE TABLE series_latest (
    series_id uuid PRIMARY KEY REFERENCES measurement_series(id),
    measurement_id bigint NOT NULL,
    calculated_at timestamptz NOT NULL,
    version bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    FOREIGN KEY (measurement_id, series_id) REFERENCES measurements(id, series_id)
);

CREATE VIEW publishable_measurements AS
SELECT m.id, m.series_id, m.observed_start_at, m.observed_end_at,
       m.value, m.source_updated_at, m.ingested_at, m.revision
FROM measurements AS m
JOIN measurement_series AS s ON s.id = m.series_id
JOIN data_sources AS d ON d.id = s.source_id
WHERE m.quality = 'accepted'
  AND s.approved = true
  AND s.data_kind = 'observed'
  AND d.permission_status = 'approved';

CREATE TABLE ingestion_checkpoints (
    provider text NOT NULL,
    stream_key text NOT NULL,
    cursor text,
    last_transport_success_at timestamptz,
    last_valid_ingestion_at timestamptz,
    last_new_observation_at timestamptz,
    coverage text NOT NULL DEFAULT 'unavailable'
        CHECK (coverage IN ('complete', 'partial', 'unavailable')),
    lease_owner text,
    lease_until timestamptz,
    version bigint NOT NULL DEFAULT 0 CHECK (version >= 0),
    PRIMARY KEY (provider, stream_key),
    CHECK ((lease_owner IS NULL) = (lease_until IS NULL))
);
CREATE INDEX ingestion_checkpoints_lease_idx
    ON ingestion_checkpoints (lease_until);

CREATE TABLE ingestion_runs (
    id uuid PRIMARY KEY,
    provider text NOT NULL,
    stream_key text NOT NULL,
    started_at timestamptz NOT NULL,
    finished_at timestamptz,
    outcome text NOT NULL CHECK (outcome IN ('running', 'complete', 'partial', 'failed')),
    accepted_count integer NOT NULL DEFAULT 0 CHECK (accepted_count >= 0),
    quarantined_count integer NOT NULL DEFAULT 0 CHECK (quarantined_count >= 0),
    http_status integer,
    CHECK (finished_at IS NULL OR finished_at >= started_at)
);
CREATE INDEX ingestion_runs_stream_started_idx
    ON ingestion_runs (provider, stream_key, started_at DESC);

CREATE TABLE quarantined_records (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    provider text NOT NULL,
    stream_key text NOT NULL,
    source_record_id text,
    reason text NOT NULL,
    payload_hash char(64) NOT NULL CHECK (payload_hash ~ '^[0-9a-f]{64}$'),
    received_at timestamptz NOT NULL
);
CREATE INDEX quarantined_records_stream_received_idx
    ON quarantined_records (provider, stream_key, received_at DESC);

INSERT INTO schema_migrations(version) VALUES ('0001_initial');
COMMIT;
