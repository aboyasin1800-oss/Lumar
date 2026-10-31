$port = 52000
$script = 'D:\YASIN\Gateway\LocalWhatsApp\src\server.mjs'
$env:LUMAR_LOCAL_GATEWAY_PORT = $port
$env:LUMAR_LOCAL_TEST_RECIPIENT = '967775106545'
$env:LUMAR_LOCAL_MEDIA_ORIGIN = 'https://public-backend.example.com'
Start-Process -FilePath 'node' -ArgumentList @($script) -WorkingDirectory 'D:\YASIN\Gateway\LocalWhatsApp' -PassThru | Out-Null
Start-Sleep -Seconds 2
$expectedScript = $script.Replace("'", "''")
$cmd = @"
$port = $port
$matchingPids = @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -and $_.CommandLine -match [regex]::Escape('$expectedScript') } | Select-Object -ExpandProperty ProcessId)
Write-Host "MATCH_COUNT:$($matchingPids.Count)"
if (-not $matchingPids) { Write-Output 'NO_MATCH'; exit 0 }
$unique = $matchingPids | Sort-Object -Unique
foreach ($pid in $unique) { Stop-Process -Id $pid -Force -ErrorAction SilentlyContinue; Write-Output \"KILL:$pid\" }
"@
Write-Host "EXPECTED: $expectedScript"
Write-Host "CMD:" 
Write-Host $cmd
Write-Host "RESULT:"
powershell -ExecutionPolicy Bypass -NoProfile -Command $cmd
Write-Host "AFTER:"
Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -match [regex]::Escape($script) } | Select-Object ProcessId,Name,CommandLine | Format-Table -AutoSize
