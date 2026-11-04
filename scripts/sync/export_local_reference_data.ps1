<#
.SYNOPSIS
  Export the refreshed reference data (Regions, Towers, UAVs, UserGeographicScopes) from the
  local DB into a self-contained SQL file that recreates them in a "sync_stage" schema.

.DESCRIPTION
  Nothing in public.* is touched, locally or remotely. The local DB only gets a temporary
  "sync_stage" schema which is dropped again at the end.
  Output: scripts/sync/out/local_reference_stage.sql  (load it on production, then run
  upsert_reference_data.sql).
#>
param(
    [string]$Container = "uavpms-db",
    [string]$User      = "uav_admin",
    [string]$Database  = "uav_pms_db",
    [string[]]$Tables  = @("Regions", "Substations", "TransmissionLines", "Towers", "AssetComponents", "UAVs", "UserGeographicScopes")
)

$ErrorActionPreference = "Stop"
$outDir  = Join-Path $PSScriptRoot "out"
$outFile = Join-Path $outDir "local_reference_stage.sql"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$copies = ($Tables | ForEach-Object { "CREATE TABLE sync_stage.`"$_`" AS SELECT * FROM public.`"$_`";" }) -join "`n"
$prepare = @"
DROP SCHEMA IF EXISTS sync_stage CASCADE;
CREATE SCHEMA sync_stage;
$copies
"@

Write-Host "1. Creating temporary sync_stage schema in local DB..."
$prepare | docker exec -i $Container psql -U $User -d $Database -v ON_ERROR_STOP=1 -q
if ($LASTEXITCODE -ne 0) { throw "Failed to build sync_stage locally" }

try {
    Write-Host "2. Dumping sync_stage -> $outFile"
    # Dump inside the container then copy out, so the UTF-8 bytes are not re-encoded by PowerShell.
    docker exec $Container pg_dump -U $User -d $Database --schema=sync_stage --no-owner --no-privileges --encoding=UTF8 -f /tmp/local_reference_stage.sql
    if ($LASTEXITCODE -ne 0) { throw "pg_dump failed" }
    docker cp "${Container}:/tmp/local_reference_stage.sql" $outFile
    docker exec $Container rm -f /tmp/local_reference_stage.sql
}
finally {
    Write-Host "3. Dropping local sync_stage schema..."
    "DROP SCHEMA IF EXISTS sync_stage CASCADE;" | docker exec -i $Container psql -U $User -d $Database -q
}

Write-Host "Done. Rows exported (local counts):"
$counts = ($Tables | ForEach-Object { "SELECT '$_', count(*) FROM public.`"$_`"" }) -join " UNION ALL "
"$counts;" | docker exec -i $Container psql -U $User -d $Database -At -F " : "
