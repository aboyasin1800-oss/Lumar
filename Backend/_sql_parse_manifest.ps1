$src = 'D:\YASIN\Backend\final_zero_baseline_manifest.sql'
$tmp = Join-Path $env:TEMP 'manifest_parse.sql'
$data = Get-Content -Raw -Path $src
Set-Content -Path $tmp -Value ('SET PARSEONLY ON;' + [Environment]::NewLine + $data)
sqlcmd -S 'YASIN-YASIN\SQLEXPRESS' -d 'LUMAR_ERP_ES_VALIDATION' -I -b -m 1 -e -i $tmp
Write-Output "EXIT:$LASTEXITCODE"
