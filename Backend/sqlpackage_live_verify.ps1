$ErrorActionPreference = 'Stop'
$server = 'YASIN-YASIN\SQLEXPRESS'
$target = 'LUMAR_ERP_TEST_R3_ROLLS'
$dac = 'D:\YASIN\Backend\LUMAR_ERP_TEST_R3_ROLLS_SCHEMA_PROBE.dacpac'
$sqlPackage = 'D:\YASIN\Backend\SqlPackage\extract\sqlpackage.exe'
$sqlCmd = 'C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\sqlcmd.exe'

& $sqlPackage /Action:Publish /SourceFile:$dac /TargetConnectionString:"Data Source=$server;Initial Catalog=$target;Integrated Security=True;Encrypt=True;TrustServerCertificate=True" /p:DropObjectsNotInSource=False
$ec = $LASTEXITCODE
Write-Host "PUBLISH_EXIT_CODE=$ec"
if ($ec -ne 0) { exit $ec }

& $sqlCmd -S $server -d $target -Q "SELECT COUNT(*) AS TEST_TABLE_COUNT FROM sys.tables; SELECT COUNT(*) AS TEST_VIEW_COUNT FROM sys.views; SELECT COUNT(*) AS TEST_PROCEDURE_COUNT FROM sys.procedures;" -W -s ","
foreach ($t in @('TailoringPieceSaleValues','FabricRolls','InventoryItems','InventoryReceiptLines','InventoryReceiptPostings','GoodsReceiptItemStorageAllocations','GoodsReceipts','GoodsReceiptItems')) {
    $v = & $sqlCmd -S $server -d $target -Q "SELECT CASE WHEN OBJECT_ID(N'dbo.$t', N'U') IS NOT NULL THEN 'YES' ELSE 'NO' END;" -h -1 -W -s ","
    Write-Host "TARGET_TABLE_EXISTS:$t=$($v.Trim())"
}
