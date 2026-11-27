#Requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [uri] $GatewayBaseUrl,

    [string] $OutputPath,

    [switch] $ReplaceExisting
)

$ErrorActionPreference = 'Stop'
$swaggerEndpoint = '/swagger/v1/swagger.json'
$swaggerTimeoutSeconds = 30
$apiVersionPrefix = '^/api/v1(?:\.0)?'
$openApiVersionPattern = '^3\.\d+\.\d+$'
$internalRoutePattern = '(?:^|/)(?<segment>ai-service|hangfire|realtime|enqueue-email|schedule)(?:/|$)'
$publicServices = [ordered]@{
    Identity = @{ Path = '/auth/login'; Method = 'post' }
    Operations = @{ Path = '/missions/my'; Method = 'get' }
    AIInspection = @{ Path = '/ai-analysis/upload'; Method = 'post' }
    Notification = @{ Path = '/notifications/unread-count'; Method = 'get' }
}

if (-not $GatewayBaseUrl.IsAbsoluteUri -or $GatewayBaseUrl.Scheme -notin @('http', 'https')) {
    throw 'GatewayBaseUrl must be an absolute HTTP or HTTPS URL.'
}

if ($GatewayBaseUrl.UserInfo -or $GatewayBaseUrl.Query -or $GatewayBaseUrl.Fragment) {
    throw 'GatewayBaseUrl must not include credentials, a query or a fragment.'
}

if ($ReplaceExisting -and [string]::IsNullOrWhiteSpace($OutputPath)) {
    throw 'ReplaceExisting requires an explicit OutputPath.'
}

$useDesktopPowerShell = $PSVersionTable.PSEdition -eq 'Desktop'
if ($useDesktopPowerShell -and -not (Get-Command node -CommandType Application -ErrorAction SilentlyContinue)) {
    throw 'Strict JSON validation requires PowerShell 7 or Windows PowerShell 5.1 with Node.js.'
}

$swaggerUri = [uri]::new($GatewayBaseUrl, $swaggerEndpoint)
try {
    $response = Invoke-WebRequest -Uri $swaggerUri -UseBasicParsing -TimeoutSec $swaggerTimeoutSeconds -MaximumRedirection 0
}
catch {
    throw "Unable to fetch $swaggerEndpoint from the Gateway. Check the Gateway and its downstream services."
}

$json = $response.Content
if ($json -isnot [string] -or $json -notmatch '^\s*\{') {
    throw 'The Gateway response is not an OpenAPI JSON object.'
}

try {
    # ConvertFrom-Json chấp nhận một số cú pháp mở rộng mà ứng dụng đọc hợp đồng sẽ từ chối.
    if ($useDesktopPowerShell) {
        $validateJson = "try { JSON.parse(Buffer.from(require('fs').readFileSync(0, 'utf8'), 'base64').toString('utf8')) } catch { process.exitCode = 1 }"
        $encodedJson = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($json))
        $encodedJson | node -e $validateJson
        if ($LASTEXITCODE -ne 0) {
            throw 'Invalid JSON.'
        }
    }
    else {
        [System.Text.Json.JsonDocument]::Parse($json).Dispose()
    }
    $openApi = ConvertFrom-Json -InputObject $json
}
catch {
    throw 'The Gateway returned invalid OpenAPI JSON.'
}

if ($openApi.openapi -isnot [string] -or $openApi.openapi -notmatch $openApiVersionPattern -or
    $openApi.paths -isnot [pscustomobject]) {
    throw 'The Gateway document must have an OpenAPI 3 version and a paths object.'
}

$paths = $openApi.paths.PSObject.Properties
foreach ($path in $paths) {
    $decodedPath = [uri]::UnescapeDataString($path.Name)
    if ($decodedPath -match $internalRoutePattern) {
        throw "OpenAPI contains an internal route segment: $($Matches.segment)."
    }
}

# Swagger gộp không ghi nguồn của từng route; operation đại diện cho biết service có mặt.
foreach ($service in $publicServices.GetEnumerator()) {
    $expectedPath = "$apiVersionPrefix$($service.Value.Path)$"
    $operation = $paths | Where-Object {
        $_.Name -match $expectedPath -and
        $_.Value -is [pscustomobject] -and
        $_.Value.PSObject.Properties[$service.Value.Method].Value -is [pscustomobject]
    } | Select-Object -First 1

    if ($null -eq $operation) {
        throw "OpenAPI is missing the public operation for $($service.Key): $($service.Value.Method) $($service.Value.Path)."
    }
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    Write-Output 'OpenAPI validation passed for all four public services.'
    return
}

$destination = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
if ([IO.File]::Exists($destination) -and -not $ReplaceExisting) {
    throw 'OutputPath already exists. Use ReplaceExisting only for an approved contract update.'
}

$directory = [IO.Path]::GetDirectoryName($destination)
if (-not [IO.Directory]::Exists($directory)) {
    throw 'The output directory must exist before export.'
}

# Ghi cùng thư mục rồi đổi tên để bản hợp đồng cũ không bị cắt dở nếu ghi lỗi.
$stagedPath = Join-Path $directory ([IO.Path]::GetRandomFileName())
try {
    [IO.File]::WriteAllText($stagedPath, $json, [Text.UTF8Encoding]::new($false))
    if ($ReplaceExisting -and [IO.File]::Exists($destination)) {
        [IO.File]::Replace($stagedPath, $destination, [System.Management.Automation.Language.NullString]::Value)
    }
    else {
        [IO.File]::Move($stagedPath, $destination)
    }
}
finally {
    [IO.File]::Delete($stagedPath)
}

Write-Output "OpenAPI exported to $destination."
