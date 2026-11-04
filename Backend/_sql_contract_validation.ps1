$server = 'YASIN-YASIN\SQLEXPRESS'
$db = 'LUMAR_ERP_ES_VALIDATION'
$files = @(
  'D:\YASIN\Backend\final_zero_baseline_manifest.sql',
  'D:\YASIN\Backend\verify_zero_baseline_post_commit.sql'
)

foreach ($src in $files) {
  $filename = [System.IO.Path]::GetFileName($src)
  $tmpParse = Join-Path $env:TEMP ("parse_${filename}")
  $data = Get-Content -Raw -Path $src
  Set-Content -Path $tmpParse -Value ('SET PARSEONLY ON;' + [Environment]::NewLine + $data)
  sqlcmd -S $server -d $db -I -b -m 1 -e -i $tmpParse
  Write-Output "PARSE:$filename EXIT:$LASTEXITCODE"

  $tmpNoExec = Join-Path $env:TEMP ("noexec_${filename}")
  Set-Content -Path $tmpNoExec -Value ('SET NOEXEC ON;' + [Environment]::NewLine + $data)
  sqlcmd -S $server -d $db -I -b -m 1 -e -i $tmpNoExec
  Write-Output "NOEXEC:$filename EXIT:$LASTEXITCODE"
  Write-Output '---'
}
