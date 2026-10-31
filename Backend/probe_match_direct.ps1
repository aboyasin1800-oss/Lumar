$port = 52000
$script = 'D:\YASIN\Gateway\LocalWhatsApp\src\server.mjs'
$env:LUMAR_LOCAL_GATEWAY_PORT = $port
$env:LUMAR_LOCAL_TEST_RECIPIENT = '967775106545'
$env:LUMAR_LOCAL_MEDIA_ORIGIN = 'https://public-backend.example.com'
Start-Process -FilePath 'node' -ArgumentList @($script) -WorkingDirectory 'D:\YASIN\Gateway\LocalWhatsApp' -PassThru | Out-Null
Start-Sleep -Seconds 2
$matchingPids = @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -and $_.CommandLine -match [regex]::Escape($script) } | Select-Object -ExpandProperty ProcessId)
Write-Host "MATCH_COUNT:$($matchingPids.Count)"
foreach ($pid in $matchingPids) {
    Write-Host "KILL:$pid"
    Stop-Process -Id $pid -Force -ErrorAction SilentlyContinue
}
Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -match [regex]::Escape($script) } | Select-Object ProcessId,Name,CommandLine | Format-Table -AutoSize
