$conns = @(Get-NetTCPConnection -LocalPort 5093 -ErrorAction SilentlyContinue)
if ($conns.Count -eq 0) {
    Write-Host 'PORT_5093_FREE=YES'
    exit 0
}

$conns | Select-Object LocalAddress,LocalPort,State,OwningProcess | Format-Table -AutoSize
$pid = $conns[0].OwningProcess
Get-CimInstance Win32_Process -Filter "ProcessId = $pid" | Select-Object ProcessId,Name,CommandLine | Format-List *
