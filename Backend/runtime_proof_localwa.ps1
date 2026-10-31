$gatewayProcs = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object { $_.Name -eq 'node.exe' -and $_.CommandLine -match 'LocalWhatsApp.*server\.mjs' }

Write-Host 'BACKEND_PORT_5093'
Get-NetTCPConnection -LocalPort 5093 -ErrorAction SilentlyContinue | Select-Object LocalAddress,LocalPort,State,OwningProcess | Format-Table -AutoSize

Write-Host 'GATEWAY_PORT_5094'
Get-NetTCPConnection -LocalPort 5094 -ErrorAction SilentlyContinue | Select-Object LocalAddress,LocalPort,State,OwningProcess | Format-Table -AutoSize

Write-Host 'GATEWAY_COUNT:'
($gatewayProcs | Measure-Object).Count

Write-Host 'GATEWAY_PIDS:'
$gatewayProcs | Select-Object ProcessId,CommandLine | Format-Table -AutoSize
