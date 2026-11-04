$server = 'YASIN-YASIN\SQLEXPRESS'
$db = 'LUMAR_ERP_ES_VALIDATION'
$files = @(
    'D:\YASIN\Backend\final_zero_baseline_manifest.sql',
    'D:\YASIN\Backend\verify_zero_baseline_post_commit.sql'
)

foreach ($file in $files) {
    $name = [System.IO.Path]::GetFileName($file)
    $parseFile = Join-Path $env:TEMP ('contract_' + $name)
    $noexecFile = Join-Path $env:TEMP ('noexec_' + $name)
    $sql = Get-Content -Raw -Path $file

    Set-Content -Path $parseFile -Value ('SET PARSEONLY ON;' + [Environment]::NewLine + $sql)
    sqlcmd -S $server -d $db -I -b -m 1 -e -i $parseFile
    $parseExit = $LASTEXITCODE

    Set-Content -Path $noexecFile -Value ('SET NOEXEC ON;' + [Environment]::NewLine + $sql)
    sqlcmd -S $server -d $db -I -b -m 1 -e -i $noexecFile
    $noexecExit = $LASTEXITCODE

    Write-Output "FILE=$name PARSE=$parseExit NOEXEC=$noexecExit"
}
