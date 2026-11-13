$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Security
$securePath = Join-Path $env:APPDATA 'com.example\tailoring_system\flutter_secure_storage.dat'
$bytes = [System.IO.File]::ReadAllBytes($securePath)
$plain = [System.Security.Cryptography.ProtectedData]::Unprotect($bytes, $null, [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
$text = [System.Text.Encoding]::UTF8.GetString($plain)
$obj = $text | ConvertFrom-Json
$token = $obj.lumar_auth_token
if (-not $token) { throw 'NO_AUTH_TOKEN_FOUND' }
$headers = @{ Authorization = "Bearer $token" }
$response = Invoke-WebRequest -Uri 'http://127.0.0.1:5093/settings/test-data-reset/readiness' -Headers $headers -Method Get -UseBasicParsing
$data = $response.Content | ConvertFrom-Json
$summary = [pscustomobject]@{
  READINESS_HTTP_STATUS = $response.StatusCode
  READINESS_SUCCESS = [bool]$data.success
  READINESS_TIMEOUT = [int]$data.configuredTimeoutSeconds
  READINESS_LIVE_MODE = [bool]$data.liveMode
  READINESS_TEST_OVERRIDE = [bool]$data.testOverride
  DATABASE_NAME = $data.databaseName
  MESSAGE = $data.message
  FAILURE_STAGE = $data.failureStage
  FAILURE_CODE = $data.failureCode
}
$summary | Format-List *
