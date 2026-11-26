[CmdletBinding()]
param(
    [string]$GatewayUrl = "http://localhost:5194/swagger/v1/swagger.json",
    [string]$FrontendContractPath = "D:\UavPms_FEE\docs\contracts\openapi.json"
)

$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$backendContractPath = Join-Path $repositoryRoot "docs\contracts\openapi.json"
$backendContractDirectory = Split-Path -Parent $backendContractPath
$temporaryPath = "$backendContractPath.tmp"

Write-Host "Downloading the aggregated OpenAPI contract from $GatewayUrl"
$response = Invoke-WebRequest -Uri $GatewayUrl -UseBasicParsing
$document = $response.Content | ConvertFrom-Json

if ([string]::IsNullOrWhiteSpace($document.openapi)) {
    throw "The response is not an OpenAPI document: missing 'openapi'."
}

if ($null -eq $document.info -or $null -eq $document.paths) {
    throw "The OpenAPI document must contain both 'info' and 'paths'."
}

New-Item -ItemType Directory -Path $backendContractDirectory -Force | Out-Null
$response.Content | Set-Content -LiteralPath $temporaryPath -Encoding utf8
Move-Item -LiteralPath $temporaryPath -Destination $backendContractPath -Force

$frontendContractDirectory = Split-Path -Parent $FrontendContractPath
if (-not (Test-Path -LiteralPath $frontendContractDirectory)) {
    throw "Frontend contract directory does not exist: $frontendContractDirectory"
}

Copy-Item -LiteralPath $backendContractPath -Destination $FrontendContractPath -Force
Write-Host "OpenAPI exported to $backendContractPath"
Write-Host "OpenAPI copied to $FrontendContractPath"
