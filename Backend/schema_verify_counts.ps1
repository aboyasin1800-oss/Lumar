$server = 'YASIN-YASIN\SQLEXPRESS'
$targetDb = 'LUMAR_ERP_TEST_R3_ROLLS'
$sqlcmd = 'C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\sqlcmd.exe'
$query = @"
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

& $sqlcmd -S $server -d $targetDb -Q $query -W -s "," -h -1
exit $LASTEXITCODE
