$tokenPath = Join-Path $env:LOCALAPPDATA 'LUMAR\LocalWhatsAppGateway\gateway-token'
if (-not (Test-Path $tokenPath)) {
  Write-Output 'POLL:1 ERROR:TOKEN_NOT_FOUND'
  exit 0
}
$token = (Get-Content $tokenPath -Raw).Trim()
for ($i = 1; $i -le 4; $i++) {
  try {
    $s = Invoke-RestMethod -Uri 'http://127.0.0.1:5094/session/status' -Headers @{ Authorization = "Bearer $token" } -Method Get
    $backend = Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object { $_.LocalPort -eq 5093 } | Select-Object -First 1
    $gateway = Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object { $_.LocalPort -eq 5094 } | Select-Object -First 1
    Write-Output "POLL:$i STATE:$($s.state) LINKED:$($s.linked) CURRENT:$($s.currentNumber) LASTERROR:$($s.lastError) BACKENDPID:$($backend.OwningProcess) GATEWAYPID:$($gateway.OwningProcess)"
  }
  catch {
    Write-Output "POLL:$i ERROR:AUTH_OR_STATUS_FAILED"
  }
  if ($i -lt 4) { Start-Sleep -Seconds 8 }
}
