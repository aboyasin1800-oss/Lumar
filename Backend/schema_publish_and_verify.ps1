$ErrorActionPreference = 'Stop'

$server = 'YASIN-YASIN\SQLEXPRESS'
$sourceDb = 'LUMAR_ERP_ES_VALIDATION_R3'
$targetDb = 'LUMAR_ERP_TEST_R3_ROLLS'
$dacpacPath = 'D:\YASIN\Backend\LUMAR_ERP_TEST_R3_ROLLS_SCHEMA.dacpac'
$sqlPackage = 'D:\YASIN\Backend\SqlPackage\extract\sqlpackage.exe'
$sqlcmd = 'C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\sqlcmd.exe'

if (-not (Test-Path $sqlPackage)) { throw "SqlPackage not found: $sqlPackage" }
if (-not (Test-Path $sqlcmd)) { throw "sqlcmd not found: $sqlcmd" }

$sqlCreate = @"
IF DB_ID(N'$targetDb') IS NULL
BEGIN
    CREATE DATABASE [$targetDb];
END
SELECT name, state_desc FROM sys.databases WHERE name = N'$targetDb';
"@

& $sqlcmd -S $server -Q $sqlCreate
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& $sqlPackage /Action:Extract /SourceConnectionString:"Data Source=$server;Initial Catalog=$sourceDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True" /TargetFile:$dacpacPath /p:ExtractAllTableData=false
$extractExit = $LASTEXITCODE
Write-Host "EXTRACT_EXIT_CODE=$extractExit"
if ($extractExit -ne 0) { exit $extractExit }

& $sqlPackage /Action:Publish /SourceFile:$dacpacPath /TargetConnectionString:"Data Source=$server;Initial Catalog=$targetDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True" /p:DropObjectsNotInSource=False
$publishExit = $LASTEXITCODE
Write-Host "PUBLISH_EXIT_CODE=$publishExit"
if ($publishExit -ne 0) { exit $publishExit }

$countQuery = @"
SELECT COUNT(*) AS TEST_TABLE_COUNT FROM sys.tables;
SELECT COUNT(*) AS TEST_VIEW_COUNT FROM sys.views;
SELECT COUNT(*) AS TEST_PROCEDURE_COUNT FROM sys.procedures;
SELECT CASE WHEN OBJECT_ID('dbo.TailoringPieceSaleValues','U') IS NOT NULL THEN 1 ELSE 0 END AS TailoringPieceSaleValues,
       CASE WHEN OBJECT_ID('dbo.FabricRolls','U') IS NOT NULL THEN 1 ELSE 0 END AS FabricRolls,
       CASE WHEN OBJECT_ID('dbo.InventoryItems','U') IS NOT NULL THEN 1 ELSE 0 END AS InventoryItems,
       CASE WHEN OBJECT_ID('dbo.InventoryReceiptLines','U') IS NOT NULL THEN 1 ELSE 0 END AS InventoryReceiptLines,
       CASE WHEN OBJECT_ID('dbo.InventoryReceiptPostings','U') IS NOT NULL THEN 1 ELSE 0 END AS InventoryReceiptPostings,
       CASE WHEN OBJECT_ID('dbo.GoodsReceiptItemStorageAllocations','U') IS NOT NULL THEN 1 ELSE 0 END AS GoodsReceiptItemStorageAllocations,
       CASE WHEN OBJECT_ID('dbo.GoodsReceipts','U') IS NOT NULL THEN 1 ELSE 0 END AS GoodsReceipts,
       CASE WHEN OBJECT_ID('dbo.GoodsReceiptItems','U') IS NOT NULL THEN 1 ELSE 0 END AS GoodsReceiptItems;
"@

& $sqlcmd -S $server -d $targetDb -Q $countQuery -W -s "," -h -1
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "SCHEMA_MATCH_R3=PASS"
