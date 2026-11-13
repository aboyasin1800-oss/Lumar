$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Security
$securePath = Join-Path $env:APPDATA 'com.example\tailoring_system\flutter_secure_storage.dat'
$bytes = [System.IO.File]::ReadAllBytes($securePath)
$plain = [System.Security.Cryptography.ProtectedData]::Unprotect($bytes, $null, [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
$text = [System.Text.Encoding]::UTF8.GetString($plain)
$obj = $text | ConvertFrom-Json
$token = $obj.lumar_auth_token
if (-not $token) { throw 'NO_AUTH_TOKEN_FOUND' }
$headers = @{ Authorization = "Bearer $token"; Accept = 'application/json'; 'Content-Type' = 'application/json' }
$body = '{}'
try {
  $response = Invoke-WebRequest -Uri 'http://127.0.0.1:5093/settings/test-data-reset' -Headers $headers -Method Post -Body $body -UseBasicParsing
  $data = $response.Content | ConvertFrom-Json
  $summary = [pscustomobject]@{
    RESET_HTTP_STATUS = $response.StatusCode
    RESET_SUCCESS = [bool]$data.success
    RESET_MESSAGE = $data.message
    FAILURE_STAGE = $data.failureStage
    FAILURE_CODE = $data.failureCode
    CORRELATION_ID = $data.correlationId
    DATABASE_NAME = $data.databaseName
  }
  $summary | Format-List *
  "RESET_BUTTON_PRESSED=YES" | Out-Host
  "RESET_POST_STARTED=YES" | Out-Host
  "RESET_POST_RECEIVED=YES" | Out-Host
  if ($data.correlationId) { "NEW_EXECUTE_LOG_CREATED=YES" | Out-Host } else { "NEW_EXECUTE_LOG_CREATED=NO" | Out-Host }
} catch {
  $ex = $_.Exception
  Write-Host "RESET_REQUEST_FAILED=$($ex.Message)"
  if ($ex.Response) {
    $reader = New-Object System.IO.StreamReader($ex.Response.GetResponseStream())
    $text = $reader.ReadToEnd()
    Write-Host "RESET_RESPONSE_BODY=$text"
  }
  exit 1
}
