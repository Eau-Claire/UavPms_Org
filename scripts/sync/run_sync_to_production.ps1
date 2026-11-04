<#
.SYNOPSIS
  Push refreshed reference data (Regions, Towers, UAVs, UserGeographicScopes) from local to
  production WITHOUT deleting anything.

.DESCRIPTION
  Steps: 1) export local -> stage file   2) full pg_dump backup of production
         3) load sync_stage on production 4) pre-check (read-only)
         5) dry run (ROLLBACK)            6) ask for confirmation, then COMMIT
  psql / pg_dump are executed inside the local postgis container, so nothing needs to be
  installed on Windows. The container must be able to reach the production DB host.

.EXAMPLE
  ./scripts/sync/run_sync_to_production.ps1 -ConnectionString "Host=1.2.3.4;Port=5432;Database=uav_pms_db;Username=...;Password=..."
  # or put DB_CONNECTION=... in the repo .env and omit -ConnectionString
#>
param(
    [string]$ConnectionString,
    [string]$Container = "uavpms-db",
    [switch]$SkipExport,
    [switch]$Yes
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")

if (-not $ConnectionString) {
    $envFile = Join-Path $repoRoot ".env"
    if (Test-Path $envFile) {
        $line = Get-Content $envFile | Where-Object { $_ -match '^\s*DB_CONNECTION\s*=' } | Select-Object -First 1
        if ($line) { $ConnectionString = ($line -split '=', 2)[1].Trim().Trim('"') }
    }
}
if (-not $ConnectionString) { throw "Provide -ConnectionString or DB_CONNECTION in $repoRoot\.env" }

# Npgsql "Host=..;Port=..;Database=..;Username=..;Password=.." -> libpq env vars
$kv = @{}
foreach ($part in $ConnectionString -split ';') {
    if ($part -match '^\s*([^=]+?)\s*=\s*(.*)$') { $kv[$Matches[1].ToLower().Replace(' ', '')] = $Matches[2] }
}
function First([string[]]$keys, [string]$default = $null) {
    foreach ($k in $keys) { if ($kv.ContainsKey($k) -and $kv[$k]) { return $kv[$k] } }
    return $default
}
$pg = [ordered]@{
    PGHOST     = First @('host', 'server')
    PGPORT     = First @('port') '5432'
    PGDATABASE = First @('database', 'db')
    PGUSER     = First @('username', 'userid', 'user')
    PGPASSWORD = First @('password', 'pwd')
}
if ($pg.PGHOST -in @('localhost', '127.0.0.1')) { $pg.PGHOST = 'host.docker.internal' }
$envArgs = $pg.GetEnumerator() | ForEach-Object { '-e'; "$($_.Key)=$($_.Value)" }
Write-Host "Target: $($pg.PGUSER)@$($pg.PGHOST):$($pg.PGPORT)/$($pg.PGDATABASE)" -ForegroundColor Cyan

function Invoke-Remote([string[]]$cmd) {
    docker exec @envArgs $Container @cmd
    if ($LASTEXITCODE -ne 0) { throw "Command failed: $($cmd -join ' ')" }
}

# 1. Export
$stage = Join-Path $PSScriptRoot "out\local_reference_stage.sql"
if (-not $SkipExport) { & (Join-Path $PSScriptRoot "export_local_reference_data.ps1") -Container $Container }
foreach ($f in @($stage, (Join-Path $PSScriptRoot "precheck_reference_data.sql"), (Join-Path $PSScriptRoot "upsert_reference_data.sql"))) {
    docker cp $f "${Container}:/tmp/$(Split-Path $f -Leaf)" | Out-Null
}

# 2. Backup
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupDir = Join-Path $repoRoot "backups"
New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
Write-Host "2. Backing up production -> backups/prod_$stamp.dump"
Invoke-Remote @('pg_dump', '-Fc', '--no-owner', '-f', "/tmp/prod_$stamp.dump")
docker cp "${Container}:/tmp/prod_$stamp.dump" (Join-Path $backupDir "prod_$stamp.dump") | Out-Null
docker exec $Container rm -f "/tmp/prod_$stamp.dump"

# 3-5. Stage, pre-check, dry run
Write-Host "3. Loading sync_stage on production"
Invoke-Remote @('psql', '-q', '-v', 'ON_ERROR_STOP=1', '-c', 'DROP SCHEMA IF EXISTS sync_stage CASCADE', '-f', '/tmp/local_reference_stage.sql')
Write-Host "4. Pre-check"
Invoke-Remote @('psql', '-f', '/tmp/precheck_reference_data.sql')
Write-Host "5. Dry run"
Invoke-Remote @('psql', '-v', 'ON_ERROR_STOP=1', '-f', '/tmp/upsert_reference_data.sql')

# 6. Apply
if (-not $Yes) {
    $answer = Read-Host "Apply for real on $($pg.PGHOST)/$($pg.PGDATABASE)? Type YES to commit"
    if ($answer -ne 'YES') {
        Invoke-Remote @('psql', '-q', '-c', 'DROP SCHEMA IF EXISTS sync_stage CASCADE')
        Write-Host "Aborted. Production unchanged (sync_stage removed)." -ForegroundColor Yellow
        return
    }
}
Write-Host "6. Applying"
Invoke-Remote @('psql', '-v', 'ON_ERROR_STOP=1', '-v', 'apply=1', '-f', '/tmp/upsert_reference_data.sql')
Write-Host "Done. Backup: backups/prod_$stamp.dump" -ForegroundColor Green
