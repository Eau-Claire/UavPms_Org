-- ==========================================================================================
-- UAV-PMS: PRE-CHECK before upserting reference data on production (READ-ONLY)
-- Requires: local_reference_stage.sql already loaded (schema sync_stage exists).
-- Every row in the "conflicts" sections must be 0, otherwise the upsert will abort.
-- ==========================================================================================
\pset format aligned

\echo '== 1. Rows to INSERT (new Id) vs UPDATE (existing Id) =='
SELECT 'Regions' AS tbl,
       count(*) FILTER (WHERE p."Id" IS NULL)     AS to_insert,
       count(*) FILTER (WHERE p."Id" IS NOT NULL) AS to_update
FROM sync_stage."Regions" s LEFT JOIN public."Regions" p ON p."Id" = s."Id"
UNION ALL
SELECT 'Substations', count(*) FILTER (WHERE p."Id" IS NULL), count(*) FILTER (WHERE p."Id" IS NOT NULL)
FROM sync_stage."Substations" s LEFT JOIN public."Substations" p ON p."Id" = s."Id"
UNION ALL
SELECT 'TransmissionLines', count(*) FILTER (WHERE p."Id" IS NULL), count(*) FILTER (WHERE p."Id" IS NOT NULL)
FROM sync_stage."TransmissionLines" s LEFT JOIN public."TransmissionLines" p ON p."Id" = s."Id"
UNION ALL
SELECT 'Towers', count(*) FILTER (WHERE p."Id" IS NULL), count(*) FILTER (WHERE p."Id" IS NOT NULL)
FROM sync_stage."Towers" s LEFT JOIN public."Towers" p ON p."Id" = s."Id"
UNION ALL
SELECT 'AssetComponents', count(*) FILTER (WHERE p."Id" IS NULL), count(*) FILTER (WHERE p."Id" IS NOT NULL)
FROM sync_stage."AssetComponents" s LEFT JOIN public."AssetComponents" p ON p."Id" = s."Id"
UNION ALL
SELECT 'UAVs', count(*) FILTER (WHERE p."Id" IS NULL), count(*) FILTER (WHERE p."Id" IS NOT NULL)
FROM sync_stage."UAVs" s LEFT JOIN public."UAVs" p ON p."Id" = s."Id"
UNION ALL
SELECT 'UserGeographicScopes', count(*) FILTER (WHERE p."Id" IS NULL), count(*) FILTER (WHERE p."Id" IS NOT NULL)
FROM sync_stage."UserGeographicScopes" s LEFT JOIN public."UserGeographicScopes" p ON p."Id" = s."Id";

\echo '== 2. CONFLICTS: same business code already used by a DIFFERENT Id on production =='
SELECT 'Regions.Code' AS key, s."Code" AS value, s."Id" AS local_id, p."Id" AS prod_id
FROM sync_stage."Regions" s JOIN public."Regions" p ON p."Code" = s."Code" AND p."Id" <> s."Id"
UNION ALL
SELECT 'TransmissionLines.Code', s."Code", s."Id", p."Id"
FROM sync_stage."TransmissionLines" s JOIN public."TransmissionLines" p ON p."Code" = s."Code" AND p."Id" <> s."Id"
UNION ALL
SELECT 'Towers.TowerCode', s."TowerCode", s."Id", p."Id"
FROM sync_stage."Towers" s JOIN public."Towers" p ON p."TowerCode" = s."TowerCode" AND p."Id" <> s."Id"
UNION ALL
SELECT 'AssetComponents.ComponentCode', s."ComponentCode", s."Id", p."Id"
FROM sync_stage."AssetComponents" s JOIN public."AssetComponents" p ON p."ComponentCode" = s."ComponentCode" AND p."Id" <> s."Id"
UNION ALL
SELECT 'UAVs.UavCode', s."UavCode", s."Id", p."Id"
FROM sync_stage."UAVs" s JOIN public."UAVs" p ON p."UavCode" = s."UavCode" AND p."Id" <> s."Id";

\echo '== 3. Scopes skipped: UserId does not exist on production =='
SELECT count(*) AS scopes_with_missing_user, count(DISTINCT s."UserId") AS missing_users
FROM sync_stage."UserGeographicScopes" s
WHERE NOT EXISTS (SELECT 1 FROM public."Users" u WHERE u."Id" = s."UserId");

\echo '== 4. Towers with LineAssetId missing on production (will keep prod value / NULL) =='
SELECT count(*) AS towers_missing_line
FROM sync_stage."Towers" s
WHERE s."LineAssetId" IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM public."TransmissionLines" t WHERE t."Id" = s."LineAssetId");

\echo '== 5. Column drift (columns in local stage but not on production) =='
SELECT s.table_name, s.column_name
FROM information_schema.columns s
WHERE s.table_schema = 'sync_stage'
  AND NOT EXISTS (SELECT 1 FROM information_schema.columns p
                  WHERE p.table_schema = 'public' AND p.table_name = s.table_name AND p.column_name = s.column_name)
ORDER BY 1, 2;

