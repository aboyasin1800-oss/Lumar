$port = 52010
$script = 'D:\YASIN\Gateway\LocalWhatsApp\src\server.mjs'
$nodeCode = 'const http=require("http"); http.createServer((_,res)=>res.end("foreign")).listen(Number(process.argv[1]), "127.0.0.1"); setInterval(() => {}, 1000);'
Start-Process -FilePath 'node' -ArgumentList @('-e', $nodeCode, $port) -PassThru | Out-Null
Start-Sleep -Seconds 2
$portOwners = @(Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique)
$matchingPids = @()
foreach ($ownerPid in $portOwners) {
  $ownerProc = Get-CimInstance Win32_Process -Filter "ProcessId = $ownerPid" -ErrorAction SilentlyContinue
  if ($ownerProc -and $ownerProc.CommandLine -match [regex]::Escape($script)) { $matchingPids += $ownerPid }
}
if (-not $matchingPids) {
  $matchingPids = @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -and $_.CommandLine -match [regex]::Escape($script) } | Select-Object -ExpandProperty ProcessId)
}
Write-Host "PORT_OWNERS:$($portOwners -join ',')"
Write-Host "MATCHING:$($matchingPids -join ',')"
if (-not $matchingPids) { Write-Output 'NO_MATCH'; exit 0 }
$unique = $matchingPids | Sort-Object -Unique
foreach ($candidatePid in $unique) {
  Write-Host "KILL:$candidatePid"
  Stop-Process -Id $candidatePid -Force -ErrorAction SilentlyContinue
}
