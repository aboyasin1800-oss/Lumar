$ErrorActionPreference = 'Stop'

$server = 'YASIN-YASIN\SQLEXPRESS'
$sourceDb = 'LUMAR_ERP_ES_VALIDATION_R3'
$targetDb = 'LUMAR_ERP_TEST_R3_ROLLS'
$dacpacPath = 'D:\YASIN\Backend\LUMAR_ERP_TEST_R3_ROLLS_SCHEMA.dacpac'
$sqlPackage = 'D:\YASIN\Backend\SqlPackage\extract\sqlpackage.exe'

if (-not (Test-Path $sqlPackage)) {
    throw "SqlPackage not found: $sqlPackage"
}

$sqlCreate = @"
IF DB_ID(N'$targetDb') IS NULL
BEGIN
    CREATE DATABASE [$targetDb];
END
SELECT name, state_desc FROM sys.databases WHERE name = N'$targetDb';
"@

sqlcmd -S $server -Q $sqlCreate

& $sqlPackage /Action:Extract /SourceConnectionString:"Data Source=$server;Initial Catalog=$sourceDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True" /TargetFile:$dacpacPath /p:ExtractAllTableData=false
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& $sqlPackage /Action:Publish /SourceFile:$dacpacPath /TargetConnectionString:"Data Source=$server;Initial Catalog=$targetDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True" /p:DropObjectsNotInSource=False
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "SCHEMA_ONLY_CLONE_READY=$targetDb"
Get-Item $dacpacPath | Select-Object FullName, Length
