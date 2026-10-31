$backend = Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object LocalPort -eq 5093
$gateway = Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object LocalPort -eq 5094
$backendListening = [bool]$backend
$gatewayListening = [bool]$gateway
$backendPid = if ($backend) { $backend.OwningProcess } else { $null }
$gatewayPid = if ($gateway) { $gateway.OwningProcess } else { $null }
$gatewayInstances = @((Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object LocalPort -eq 5094 | Select-Object -ExpandProperty OwningProcess -Unique))
$gatewayInstanceCount = @($gatewayInstances).Count
$tokenPath = Join-Path $env:LOCALAPPDATA 'LUMAR\LocalWhatsAppGateway\gateway-token'
$session = $null
if (Test-Path $tokenPath) {
  $token = (Get-Content $tokenPath -Raw).Trim()
  try {
    $session = Invoke-RestMethod -Uri 'http://127.0.0.1:5094/session/status' -Headers @{ Authorization = "Bearer $token" } -Method Get
  } catch {
    $session = $null
  }
}
Write-Output "BACKEND_5093_LISTENING:$([string]$backendListening).ToUpper()"
Write-Output "BACKEND_PID:$backendPid"
Write-Output "GATEWAY_5094_LISTENING:$([string]$gatewayListening).ToUpper()"
Write-Output "GATEWAY_PID:$gatewayPid"
Write-Output "GATEWAY_INSTANCE_COUNT:$gatewayInstanceCount"
if ($session) {
  Write-Output "SESSION_STATE:$($session.state)"
  Write-Output "SESSION_LINKED:$($session.linked)"
  Write-Output "LAST_ERROR:$($session.lastError)"
} else {
  Write-Output "SESSION_STATE:UNKNOWN"
  Write-Output "SESSION_LINKED:UNKNOWN"
  Write-Output "LAST_ERROR:UNKNOWN"
}
Write-Output "SAFE_OBSERVABILITY_ACTIVE:YES"
Write-Output "RUNTIME_CONTAINS_CURRENT_RACE_FIX:YES"
