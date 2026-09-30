SET XACT_ABORT ON;
IF DB_NAME() NOT IN(N'LUMAR_ERP_TEST',N'LUMAR_ERP_ES_VALIDATION') THROW 52500,N'ES-7B-3 raw supplier invoice lines are restricted to approved ES validation databases.',1;
BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.SupplierInvoiceLines',N'U') IS NULL THROW 52501,N'SupplierInvoiceLines is required before the ES-7B-3 raw line extension.',1;

    IF COL_LENGTH(N'dbo.SupplierInvoiceLines',N'ItemDescription') IS NULL
        ALTER TABLE dbo.SupplierInvoiceLines ADD ItemDescription nvarchar(200) NULL;
    IF COL_LENGTH(N'dbo.SupplierInvoiceLines',N'ItemType') IS NULL
        ALTER TABLE dbo.SupplierInvoiceLines ADD ItemType nvarchar(30) NULL;
    IF COL_LENGTH(N'dbo.SupplierInvoiceLines',N'SupplierItemCode') IS NULL
        ALTER TABLE dbo.SupplierInvoiceLines ADD SupplierItemCode nvarchar(100) NULL;

    IF EXISTS(SELECT 1 FROM dbo.SupplierInvoiceLines WHERE InventoryItemId IS NOT NULL)
    BEGIN
        EXEC(N'UPDATE line
        SET ItemDescription=COALESCE(NULLIF(line.ItemDescription,N''''),item.ItemName,N''غير محدد''),
            ItemType=COALESCE(NULLIF(line.ItemType,N''''),CASE
                WHEN item.FabricCategory IS NOT NULL OR item.Category LIKE N''%Fabric%'' THEN N''Fabric''
                WHEN item.Category LIKE N''%Tool%'' OR item.Category LIKE N''%Accessory%'' OR item.Category LIKE N''%Thread%'' THEN N''UsedTool''
                ELSE N''Legacy''
            END),
            SupplierItemCode=COALESCE(NULLIF(line.SupplierItemCode,N''''),item.ItemCode)
        FROM dbo.SupplierInvoiceLines line
        LEFT JOIN dbo.InventoryItems item ON item.InventoryItemID=line.InventoryItemId
        WHERE line.ItemDescription IS NULL OR line.ItemType IS NULL OR line.SupplierItemCode IS NULL;');
    END;

    EXEC(N'UPDATE dbo.SupplierInvoiceLines
    SET ItemDescription=COALESCE(NULLIF(ItemDescription,N''''),N''غير محدد''),
        ItemType=COALESCE(NULLIF(ItemType,N''''),N''Legacy'')
    WHERE ItemDescription IS NULL OR ItemType IS NULL;');

    ALTER TABLE dbo.SupplierInvoiceLines ALTER COLUMN InventoryItemId int NULL;
    EXEC(N'ALTER TABLE dbo.SupplierInvoiceLines ALTER COLUMN ItemDescription nvarchar(200) NULL;');
    EXEC(N'ALTER TABLE dbo.SupplierInvoiceLines ALTER COLUMN ItemType nvarchar(30) NULL;');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
