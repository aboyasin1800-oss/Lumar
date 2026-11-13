$conns = @(Get-NetTCPConnection -LocalPort 5093 -ErrorAction SilentlyContinue)
if ($conns.Count -eq 0) {
    Write-Host 'PORT_5093_FREE=YES'
    exit 0
}
$oPid = [int]$conns[0].OwningProcess
Get-CimInstance Win32_Process -Filter "ProcessId = $oPid" | Select-Object ProcessId,Name,CommandLine | Format-List *
