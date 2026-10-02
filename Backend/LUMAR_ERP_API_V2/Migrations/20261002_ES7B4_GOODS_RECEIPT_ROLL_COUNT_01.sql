SET XACT_ABORT ON;
IF DB_NAME() NOT IN(N'LUMAR_ERP_TEST',N'LUMAR_ERP_ES_VALIDATION') THROW 52540,N'ES-7B4 goods receipt roll count is restricted to approved ES validation databases.',1;
BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'dbo.GoodsReceiptItems',N'RollCount') IS NULL
        ALTER TABLE dbo.GoodsReceiptItems ADD RollCount int NULL;

    IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE name=N'CK_GoodsReceiptItems_ES7B4_RollCount')
        ALTER TABLE dbo.GoodsReceiptItems DROP CONSTRAINT CK_GoodsReceiptItems_ES7B4_RollCount;

    IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name=N'CK_GoodsReceiptItems_ES7B4_RollCount')
        EXEC sys.sp_executesql N'ALTER TABLE dbo.GoodsReceiptItems ADD CONSTRAINT CK_GoodsReceiptItems_ES7B4_RollCount CHECK(RollCount IS NULL OR RollCount>0);';

    IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.GoodsReceiptItems') AND name=N'IX_GoodsReceiptItems_RollCount')
        CREATE INDEX IX_GoodsReceiptItems_RollCount ON dbo.GoodsReceiptItems(RollCount);

    EXEC sys.sp_executesql N'
        UPDATE i
        SET RollCount = sil.RollCount
        FROM dbo.GoodsReceiptItems i
        INNER JOIN dbo.SupplierInvoiceLines sil ON sil.SupplierInvoiceLineId = i.SupplierInvoiceLineId
        WHERE i.RollCount IS NULL AND sil.RollCount IS NOT NULL;';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
