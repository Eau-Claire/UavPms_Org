-- ==========================================================================================
-- UAV PMS: PHASE 5 NON-DESTRUCTIVE DATABASE MIGRATION & BACKWARD COMPATIBILITY SCRIPT
-- Migration: 001_non_destructive_tower_migration_and_views.sql
-- Strategy: Zero-downtime, non-destructive migration transitioning to Tower-Centric Model.
-- Retention Policy: Retain legacy Assets/Substations/Lines for >= 2 weeks before deprecation.
-- ==========================================================================================

BEGIN;

-- ------------------------------------------------------------------------------------------
-- 1. Ensure PostGIS and Target Towers Table Structure
-- ------------------------------------------------------------------------------------------
CREATE EXTENSION IF NOT EXISTS postgis;

CREATE TABLE IF NOT EXISTS towers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tower_number VARCHAR(50) NOT NULL,
    tower_code VARCHAR(100),
    region_id UUID,
    management_unit_id UUID,
    transmission_line_id UUID,
    geom geometry(Point, 4326),
    latitude DOUBLE PRECISION,
    longitude DOUBLE PRECISION,
    height_meters DOUBLE PRECISION DEFAULT 30.0,
    tower_type VARCHAR(50) DEFAULT 'Suspension',
    voltage_level VARCHAR(20) DEFAULT '220kV',
    status VARCHAR(50) DEFAULT 'Active',
    risk_level VARCHAR(50) DEFAULT 'Low',
    active_defects_count INT DEFAULT 0,
    version OID DEFAULT 0,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    is_deleted BOOLEAN DEFAULT FALSE
);

CREATE INDEX IF NOT EXISTS idx_towers_geom ON towers USING GIST (geom);
CREATE INDEX IF NOT EXISTS idx_towers_region_id ON towers(region_id);
CREATE INDEX IF NOT EXISTS idx_towers_status ON towers(status);

-- ------------------------------------------------------------------------------------------
-- 2. Data Migration: Copy & Upsert Legacy Assets (Poles/Towers) into Towers
-- ------------------------------------------------------------------------------------------
DO $$
BEGIN
    IF EXISTS (SELECT FROM information_schema.tables WHERE table_name = 'assets') THEN
        INSERT INTO towers (
            id,
            tower_number,
            tower_code,
            region_id,
            geom,
            status,
            active_defects_count,
            created_at,
            updated_at,
            is_deleted
        )
        SELECT 
            a.id,
            COALESCE(a.asset_code, 'TOW-' || SUBSTRING(a.id::text, 1, 8)) AS tower_number,
            COALESCE(a.name, a.asset_code, 'Tower') AS tower_code,
            a.region_id,
            a.geom,
            COALESCE(a.status, 'Active'),
            COALESCE(a.active_defects_count, 0),
            COALESCE(a.created_at, NOW()),
            COALESCE(a.updated_at, NOW()),
            COALESCE(a.is_deleted, FALSE)
        FROM assets a
        ON CONFLICT (id) DO UPDATE SET
            tower_number = EXCLUDED.tower_number,
            tower_code = EXCLUDED.tower_code,
            geom = COALESCE(EXCLUDED.geom, towers.geom),
            updated_at = NOW();
    END IF;
END $$;

-- ------------------------------------------------------------------------------------------
-- 3. Data Migration: Backfill Tower Foreign Keys for Media, Anomalies, and Maintenance
-- ------------------------------------------------------------------------------------------
DO $$
BEGIN
    -- InspectionMedia: backfill tower_id from asset_id
    IF EXISTS (
        SELECT FROM information_schema.columns 
        WHERE table_name = 'inspection_media' AND column_name = 'asset_id'
    ) THEN
        UPDATE inspection_media im
        SET tower_id = im.asset_id
        WHERE im.tower_id IS NULL AND im.asset_id IS NOT NULL;
    END IF;

    -- DetectedAnomalies: backfill tower_id from asset_id
    IF EXISTS (
        SELECT FROM information_schema.columns 
        WHERE table_name = 'detected_anomalies' AND column_name = 'asset_id'
    ) THEN
        UPDATE detected_anomalies da
        SET tower_id = da.asset_id::text
        WHERE (da.tower_id IS NULL OR da.tower_id = '') AND da.asset_id IS NOT NULL;
    END IF;

    -- MaintenanceTickets: backfill tower_id from asset_id
    IF EXISTS (
        SELECT FROM information_schema.columns 
        WHERE table_name = 'maintenance_tickets' AND column_name = 'asset_id'
    ) THEN
        UPDATE maintenance_tickets mt
        SET tower_id = mt.asset_id
        WHERE mt.tower_id IS NULL AND mt.asset_id IS NOT NULL;
    END IF;
END $$;

-- ------------------------------------------------------------------------------------------
-- 4. Backward-Compatible PostgreSQL Views for Legacy Consumers
-- ------------------------------------------------------------------------------------------
CREATE OR REPLACE VIEW v_assets_compat AS
SELECT 
    t.id AS id,
    t.tower_number AS asset_code,
    t.tower_code AS name,
    'Tower' AS type,
    t.region_id AS region_id,
    t.status AS status,
    t.geom AS geom,
    t.active_defects_count AS active_defects_count,
    t.created_at AS created_at,
    t.updated_at AS updated_at,
    t.is_deleted AS is_deleted
FROM towers t;

CREATE OR REPLACE VIEW v_substations_compat AS
SELECT 
    s.id AS id,
    s.name AS name,
    s.code AS code,
    s.voltage_level AS voltage_level,
    s.region_asset_id AS region_asset_id,
    s.geom AS geom,
    s.created_at AS created_at,
    s.updated_at AS updated_at,
    s.is_deleted AS is_deleted
FROM substations s;

CREATE OR REPLACE VIEW v_transmission_lines_compat AS
SELECT 
    tl.id AS id,
    tl.name AS name,
    tl.code AS code,
    tl.voltage_level AS voltage_level,
    tl.region_id AS region_id,
    tl.geom AS geom,
    tl.created_at AS created_at,
    tl.updated_at AS updated_at,
    tl.is_deleted AS is_deleted
FROM transmission_lines tl;

-- ------------------------------------------------------------------------------------------
-- 5. Dual-Write Trigger: Keeps legacy Assets in sync when Towers are modified
-- ------------------------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION trg_sync_tower_to_asset()
RETURNS TRIGGER AS $$
BEGIN
    IF EXISTS (SELECT FROM information_schema.tables WHERE table_name = 'assets') THEN
        INSERT INTO assets (id, asset_code, name, type, region_id, geom, status, active_defects_count, updated_at, is_deleted)
        VALUES (
            NEW.id,
            NEW.tower_number,
            COALESCE(NEW.tower_code, NEW.tower_number),
            'Tower',
            NEW.region_id,
            NEW.geom,
            NEW.status,
            NEW.active_defects_count,
            NOW(),
            NEW.is_deleted
        )
        ON CONFLICT (id) DO UPDATE SET
            asset_code = EXCLUDED.asset_code,
            name = EXCLUDED.name,
            geom = COALESCE(EXCLUDED.geom, assets.geom),
            status = EXCLUDED.status,
            active_defects_count = EXCLUDED.active_defects_count,
            updated_at = NOW(),
            is_deleted = EXCLUDED.is_deleted;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_sync_towers_assets ON towers;
CREATE TRIGGER trg_sync_towers_assets
AFTER INSERT OR UPDATE ON towers
FOR EACH ROW EXECUTE FUNCTION trg_sync_tower_to_asset();

COMMIT;

-- ==========================================================================================
-- ROLLBACK SCRIPT (Execute in case of emergency rollback to pre-Phase 5 state):
-- ==========================================================================================
/*
BEGIN;
DROP TRIGGER IF EXISTS trg_sync_towers_assets ON towers;
DROP FUNCTION IF EXISTS trg_sync_tower_to_asset();
DROP VIEW IF EXISTS v_assets_compat;
DROP VIEW IF EXISTS v_substations_compat;
DROP VIEW IF EXISTS v_transmission_lines_compat;
COMMIT;
*/
