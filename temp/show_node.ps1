Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'node.exe' } | Select-Object ProcessId,Name,CommandLine | Format-Table -AutoSize | Out-String -Width 200
