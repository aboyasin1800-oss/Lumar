$ErrorActionPreference = 'Stop'
$start = Get-Date
Set-Location 'D:\YASIN\Backend'
$logPath = 'D:\YASIN\Backend\fabric_rolls_r3_after_db_fix.log'
$trxName = 'fabric_rolls_r3_after_db_fix.trx'
$projectPath = 'D:\YASIN\Backend\LUMAR_ERP_API_V2.Tests\LUMAR_ERP_API_V2.Tests.csproj'

if (-not (Test-Path $projectPath)) {
    throw "Project file not found: $projectPath"
}

& dotnet test $projectPath --filter 'FabricConsumablesFoundationIntegrationTests' --logger "trx;LogFileName=$trxName" *> $logPath
$exit = $LASTEXITCODE
$end = Get-Date
$dur = ($end - $start).ToString('hh\:mm\:ss')
Write-Host "EXIT_CODE=$exit"
Write-Host "DURATION=$dur"
exit $exit
