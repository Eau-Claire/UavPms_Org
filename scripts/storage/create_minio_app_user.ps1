# Creates (or rotates) the MinIO user the services use, attaches the built-in readwrite policy,
# checks the new key works, and writes the GitHub secrets + workflow inputs to a text file on the Desktop.
#
# Run from PowerShell:  powershell -ExecutionPolicy Bypass -File scripts\storage\create_minio_app_user.ps1
# Needs Docker; mc runs from the MinIO image, so nothing is installed. The root password is typed at the
# prompt and only passed to the container through an environment variable.
param(
    [string]$Endpoint = "116.118.3.58:9000",
    [string]$RootUser = "admin",
    [string]$AppUser = "uavpms-app",
    [string]$Bucket = "uav-media",
    [string]$Image = "bitnamilegacy/minio:2025.4.22",
    [string]$OutFile = (Join-Path ([Environment]::GetFolderPath('Desktop')) "uavpms-minio-config.txt")
)

$ErrorActionPreference = "Stop"

function New-Secret([int]$Length = 40) {
    $chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789".ToCharArray()
    $bytes = New-Object byte[] $Length
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    -join ($bytes | ForEach-Object { $chars[$_ % $chars.Length] })
}

$secure = Read-Host "MinIO root password for '$RootUser'" -AsSecureString
$rootPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
$appSecret = New-Secret

$env:MC_HOST_root = "http://$([uri]::EscapeDataString($RootUser)):$([uri]::EscapeDataString($rootPassword))@$Endpoint"
$env:MC_HOST_app = "http://$([uri]::EscapeDataString($AppUser)):$appSecret@$Endpoint"
$env:APP_USER = $AppUser
$env:APP_SECRET = $appSecret

# 'policy attach' fails when the policy is already attached; that case is fine on a re-run.
$commands = @'
set -e
mc admin user add root "$APP_USER" "$APP_SECRET"
mc admin policy attach root readwrite --user "$APP_USER" 2>&1 | grep -v -i "already" || true
mc admin user info root "$APP_USER"
mc ls app >/dev/null && echo "OK: new key can list buckets"
'@

try {
    docker run --rm -e MC_HOST_root -e MC_HOST_app -e APP_USER -e APP_SECRET --entrypoint sh $Image -c $commands
    if ($LASTEXITCODE -ne 0) { throw "mc failed (exit $LASTEXITCODE); nothing was written to $OutFile" }
}
finally {
    Remove-Item Env:MC_HOST_root, Env:MC_HOST_app, Env:APP_USER, Env:APP_SECRET -ErrorAction SilentlyContinue
    $rootPassword = $null
}

@"
UavPms - MinIO config (created $(Get-Date -Format 'yyyy-MM-dd HH:mm'))
Keep this file private. Delete it after the secrets are saved in GitHub.

1) GitHub: Settings -> Environments -> production -> Environment secrets
   MINIO_ACCESS_KEY = $AppUser
   MINIO_SECRET_KEY = $appSecret

2) Actions -> Configure MinIO Env
   endpoint        = $Endpoint
   bucket          = $Bucket
   use_ssl         = false
   public_base_url = (empty)
   restart         = true
   confirmation    = CONFIGURE_MINIO_ENV

3) Actions -> Copy Media To MinIO
   mode = dry-run, then mode = apply
   confirmation = COPY_MEDIA_TO_MINIO

Lines the workflow writes to the server .env (for reference):
   MINIO_ENDPOINT=$Endpoint
   MINIO_ACCESS_KEY=$AppUser
   MINIO_SECRET_KEY=$appSecret
   MINIO_BUCKET=$Bucket
   MINIO_USE_SSL=false
   MINIO_PUBLIC_BASE_URL=
"@ | Set-Content -Path $OutFile -Encoding UTF8

Write-Host "Saved: $OutFile"
