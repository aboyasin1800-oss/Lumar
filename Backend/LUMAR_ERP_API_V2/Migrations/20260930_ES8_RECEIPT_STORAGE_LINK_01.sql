SET XACT_ABORT ON;
IF DB_NAME() NOT IN(N'LUMAR_ERP_TEST',N'LUMAR_ERP_ES_VALIDATION') THROW 52540,N'ES-8 receipt storage link is restricted to approved ES validation databases.',1;
BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'dbo.GoodsReceiptItems',N'ItemType') IS NULL
        ALTER TABLE dbo.GoodsReceiptItems ADD ItemType nvarchar(30) NULL;

    IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE name=N'CK_GoodsReceiptItems_ES8_ItemType')
        ALTER TABLE dbo.GoodsReceiptItems DROP CONSTRAINT CK_GoodsReceiptItems_ES8_ItemType;

    EXEC sys.sp_executesql N'
        UPDATE i
        SET ItemType = CASE sil.ItemType
            WHEN N''Fabric'' THEN N''Fabric''
            WHEN N''ImportedProduct'' THEN N''ImportedProduct''
            WHEN N''UsedTool'' THEN N''UsedTool''
            ELSE i.ItemType END
        FROM dbo.GoodsReceiptItems i
        LEFT JOIN dbo.SupplierInvoiceLines sil ON sil.SupplierInvoiceLineId=i.SupplierInvoiceLineId
        WHERE i.ItemType IS NULL;';

    IF OBJECT_ID(N'dbo.GoodsReceiptItemStorageAllocations',N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.GoodsReceiptItemStorageAllocations
        (
            StorageAllocationId bigint IDENTITY(1,1) NOT NULL,
            GoodsReceiptItemId int NOT NULL,
            ItemType nvarchar(30) NOT NULL,
            StorageOperationId uniqueidentifier NOT NULL,
            StoredQuantity decimal(18,6) NOT NULL,
            InventoryItemId int NULL,
            InventoryTransactionId int NULL,
            AccountingEventId bigint NULL,
            InventoryReceiptPostingId bigint NULL,
            ImportedReadyMadeInventoryReceiptId bigint NULL,
            CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_GoodsReceiptItemStorageAllocations_CreatedAt DEFAULT SYSUTCDATETIME(),
            CONSTRAINT PK_GoodsReceiptItemStorageAllocations PRIMARY KEY(StorageAllocationId),
            CONSTRAINT FK_GoodsReceiptItemStorageAllocations_Item FOREIGN KEY(GoodsReceiptItemId) REFERENCES dbo.GoodsReceiptItems(GoodsReceiptItemId),
            CONSTRAINT FK_GoodsReceiptItemStorageAllocations_InventoryItem FOREIGN KEY(InventoryItemId) REFERENCES dbo.InventoryItems(InventoryItemID),
            CONSTRAINT CK_GoodsReceiptItemStorageAllocations_Type CHECK(ItemType IN(N'Fabric',N'ImportedProduct',N'UsedTool')),
            CONSTRAINT CK_GoodsReceiptItemStorageAllocations_Quantity CHECK(StoredQuantity>0),
            CONSTRAINT UQ_GoodsReceiptItemStorageAllocations_Operation UNIQUE(StorageOperationId)
        );
    END;

    IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.GoodsReceiptItemStorageAllocations') AND name=N'IX_GoodsReceiptItemStorageAllocations_Item')
        CREATE INDEX IX_GoodsReceiptItemStorageAllocations_Item ON dbo.GoodsReceiptItemStorageAllocations(GoodsReceiptItemId);

    IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name=N'CK_GoodsReceiptItems_ES8_ItemType')
        EXEC sys.sp_executesql N'ALTER TABLE dbo.GoodsReceiptItems ADD CONSTRAINT CK_GoodsReceiptItems_ES8_ItemType CHECK(ItemType IS NULL OR ItemType IN(N''Fabric'',N''ImportedProduct'',N''UsedTool''));';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
