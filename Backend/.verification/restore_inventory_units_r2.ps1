$query = @'
IF NOT EXISTS (SELECT 1 FROM dbo.InventoryUnitCatalog WHERE UnitId = 1)
    INSERT INTO dbo.InventoryUnitCatalog (UnitId, Code, NameAr, InchesPerUnit)
    VALUES (1, N'YARD', N'ياردة', 36.000000);
IF NOT EXISTS (SELECT 1 FROM dbo.InventoryUnitCatalog WHERE UnitId = 2)
    INSERT INTO dbo.InventoryUnitCatalog (UnitId, Code, NameAr, InchesPerUnit)
    VALUES (2, N'INCH', N'بوصة', 1.000000);
IF NOT EXISTS (SELECT 1 FROM dbo.InventoryUnitCatalog WHERE UnitId = 3)
    INSERT INTO dbo.InventoryUnitCatalog (UnitId, Code, NameAr, InchesPerUnit)
    VALUES (3, N'PIECE', N'قطعة', NULL);
SELECT UnitId, Code, NameAr, InchesPerUnit FROM dbo.InventoryUnitCatalog ORDER BY UnitId;
'@
& sqlcmd -S YASIN-YASIN\SQLEXPRESS -d LUMAR_ERP_ES_VALIDATION_R2 -E -Q $query -W -s '|'
