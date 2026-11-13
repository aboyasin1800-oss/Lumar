param([int]$Pid)
Get-CimInstance Win32_Process -Filter "ProcessId = $Pid" | Select-Object ProcessId,Name,CommandLine | Format-List *
