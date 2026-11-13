param([int]$TargetPid)
Get-CimInstance Win32_Process -Filter "ProcessId = $TargetPid" | Select-Object ProcessId,Name,CommandLine | Format-List *
