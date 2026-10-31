$p = Get-CimInstance Win32_Process | Where-Object {
    $_.Name -match 'dotnet|node' -and $_.CommandLine -match 'LUMAR_ERP_API_V2|LocalWhatsApp|local-whatsapp'
}
foreach ($x in $p) {
    Stop-Process -Id $x.ProcessId -Force -ErrorAction SilentlyContinue
    Write-Output "KILL:$($x.ProcessId):$($x.Name):$($x.CommandLine)"
}
