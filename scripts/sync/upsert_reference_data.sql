-- ==========================================================================================
-- UAV-PMS: NON-DESTRUCTIVE UPSERT of refreshed reference data into production
-- Tables : Regions, Towers, UAVs, UserGeographicScopes
-- Source : schema sync_stage (loaded from scripts/sync/out/local_reference_stage.sql)
--
-- Rules
--   * NO DELETE / TRUNCATE / DROP on public.*. Rows that exist only on production are kept.
--   * Match on "Id": existing rows are UPDATED, missing rows are INSERTED.
--   * Live / operational columns are never overwritten on existing rows (see skip lists).
--   * Only columns that exist in BOTH local and production are synced (schema drift safe).
--   * Whole run is one transaction. Business-key conflicts (Code/TowerCode/UavCode) abort it.
--
-- Usage
--   Dry run (default, ROLLBACK at the end):
--     psql "<conn>" -v ON_ERROR_STOP=1 -f upsert_reference_data.sql
--   Apply for real (COMMIT):
--     psql "<conn>" -v ON_ERROR_STOP=1 -v apply=1 -f upsert_reference_data.sql
-- ==========================================================================================
\set ON_ERROR_STOP 1
SET client_encoding = 'UTF8';

BEGIN;

-- Generic "upsert by Id" helper (session-local, disappears after disconnect).
CREATE FUNCTION pg_temp.upsert_by_id(
    p_table       text,
    p_skip_update text[] DEFAULT '{}',   -- columns never overwritten on existing rows
    p_extra_set   text   DEFAULT NULL,   -- extra SET expression for existing rows
    p_where       text   DEFAULT ''      -- filter on staged rows (alias s)
) RETURNS bigint
LANGUAGE plpgsql AS $fn$
DECLARE
    v_cols text;
    v_sel  text;
    v_set  text;
    v_n    bigint;
BEGIN
    SELECT string_agg(format('%I', p.column_name), ', ' ORDER BY p.ordinal_position),
           string_agg(format('s.%I', p.column_name), ', ' ORDER BY p.ordinal_position),
           string_agg(format('%I = EXCLUDED.%I', p.column_name, p.column_name), ', ' ORDER BY p.ordinal_position)
               FILTER (WHERE p.column_name <> ALL (ARRAY['Id', 'CreatedAt', 'CreatedBy'] || p_skip_update))
      INTO v_cols, v_sel, v_set
      FROM information_schema.columns p
      JOIN information_schema.columns s
        ON s.table_schema = 'sync_stage' AND s.table_name = p.table_name AND s.column_name = p.column_name
     WHERE p.table_schema = 'public' AND p.table_name = p_table;

    IF v_cols IS NULL THEN
        RAISE EXCEPTION 'No common columns for table %', p_table;
    END IF;

    IF p_extra_set IS NOT NULL THEN
        v_set := concat_ws(', ', v_set, p_extra_set);
    END IF;

    EXECUTE format(
        'INSERT INTO public.%1$I AS t (%2$s) SELECT %3$s FROM sync_stage.%1$I s %4$s
         ON CONFLICT ("Id") DO UPDATE SET %5$s',
        p_table, v_cols, v_sel, p_where, v_set);

    GET DIAGNOSTICS v_n = ROW_COUNT;
    RAISE NOTICE '% : % row(s) inserted/updated', p_table, v_n;
    RETURN v_n;
END;
$fn$;

-- Guard: abort if a business code is already owned by a different Id on production.
DO $$
DECLARE v_conflicts int;
BEGIN
    SELECT count(*) INTO v_conflicts FROM (
        SELECT 1 FROM sync_stage."Regions" s JOIN public."Regions" p ON p."Code" = s."Code" AND p."Id" <> s."Id"
        UNION ALL
        SELECT 1 FROM sync_stage."TransmissionLines" s JOIN public."TransmissionLines" p ON p."Code" = s."Code" AND p."Id" <> s."Id"
        UNION ALL
        SELECT 1 FROM sync_stage."Towers" s JOIN public."Towers" p ON p."TowerCode" = s."TowerCode" AND p."Id" <> s."Id"
        UNION ALL
        SELECT 1 FROM sync_stage."AssetComponents" s JOIN public."AssetComponents" p ON p."ComponentCode" = s."ComponentCode" AND p."Id" <> s."Id"
        UNION ALL
        SELECT 1 FROM sync_stage."UAVs" s JOIN public."UAVs" p ON p."UavCode" = s."UavCode" AND p."Id" <> s."Id"
    ) c;
    IF v_conflicts > 0 THEN
        RAISE EXCEPTION '% business-code conflict(s). Run precheck_reference_data.sql section 2.', v_conflicts;
    END IF;
END $$;

-- Sanitize foreign keys in staging so FK constraints are never violated on production
UPDATE sync_stage."Substations" s SET "RegionAssetId" = NULL
WHERE s."RegionAssetId" IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM public."Regions" r WHERE r."Id" = s."RegionAssetId")
  AND NOT EXISTS (SELECT 1 FROM sync_stage."Regions" r WHERE r."Id" = s."RegionAssetId");

UPDATE sync_stage."TransmissionLines" s SET "ManagementUnitId" = NULL
WHERE s."ManagementUnitId" IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM public."ManagementUnits" m WHERE m."Id" = s."ManagementUnitId");

UPDATE sync_stage."TransmissionLines" s SET "SubstationAssetId" = NULL
WHERE s."SubstationAssetId" IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM public."Substations" sub WHERE sub."Id" = s."SubstationAssetId")
  AND NOT EXISTS (SELECT 1 FROM sync_stage."Substations" sub WHERE sub."Id" = s."SubstationAssetId");

-- Towers: never point to a TransmissionLine that does not exist
UPDATE sync_stage."Towers" s SET "LineAssetId" = NULL
WHERE s."LineAssetId" IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM public."TransmissionLines" t WHERE t."Id" = s."LineAssetId")
  AND NOT EXISTS (SELECT 1 FROM sync_stage."TransmissionLines" t WHERE t."Id" = s."LineAssetId");

-- 1. Regions (self FK ParentId is validated at end of statement, so one pass is enough)
SELECT pg_temp.upsert_by_id('Regions');

-- 2. Substations
SELECT pg_temp.upsert_by_id('Substations');

-- 3. TransmissionLines
SELECT pg_temp.upsert_by_id('TransmissionLines');

-- 4. Towers: keep production line link and live counters
SELECT pg_temp.upsert_by_id('Towers',
    ARRAY['LineAssetId', 'ActiveDefectsCount', 'LastInspectedAt']);

-- 5. AssetComponents
SELECT pg_temp.upsert_by_id('AssetComponents');

-- 6. UAVs: update identity / assignment only, keep live state; bump concurrency Version
SELECT pg_temp.upsert_by_id('UAVs',
    ARRAY['Status', 'OperationalStatus', 'TechnicalHealth', 'TechnicalHealthUpdatedAt',
          'BatteryLevel', 'LastTechnicalInspectionId', 'LastMaintenanceAt', 'Version'],
    '"Version" = COALESCE(t."Version", 0) + 1');

-- 7. UserGeographicScopes: only for users that exist on production and only when the
--    same (UserId, RegionId) scope is not already present under another Id.
SELECT pg_temp.upsert_by_id('UserGeographicScopes', '{}', NULL,
    $w$WHERE EXISTS (SELECT 1 FROM public."Users" u WHERE u."Id" = s."UserId")
         AND NOT EXISTS (SELECT 1 FROM public."UserGeographicScopes" x
                         WHERE x."UserId" = s."UserId"
                           AND x."RegionId" IS NOT DISTINCT FROM s."RegionId"
                           AND x."Id" <> s."Id"
                           AND NOT x."IsDeleted")$w$);

-- Staging data is no longer needed.
DROP SCHEMA sync_stage CASCADE;

\echo '== Row counts after upsert (inside transaction) =='
SELECT 'Regions' AS tbl, count(*) FROM public."Regions"
UNION ALL SELECT 'Substations', count(*) FROM public."Substations"
UNION ALL SELECT 'TransmissionLines', count(*) FROM public."TransmissionLines"
UNION ALL SELECT 'Towers', count(*) FROM public."Towers"
UNION ALL SELECT 'AssetComponents', count(*) FROM public."AssetComponents"
UNION ALL SELECT 'UAVs', count(*) FROM public."UAVs"
UNION ALL SELECT 'UserGeographicScopes', count(*) FROM public."UserGeographicScopes";

\if :{?apply}
    COMMIT;
    \echo '>>> COMMITTED'
\else
    ROLLBACK;
    \echo '>>> DRY RUN - rolled back, sync_stage kept (re-run with -v apply=1 to commit)'
\endif
