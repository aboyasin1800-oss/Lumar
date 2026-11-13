SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;

DECLARE @CurrentDatabase sysname = DB_NAME();

IF @CurrentDatabase NOT IN (N'LUMAR_ERP_ES_VALIDATION_R3', N'LUMAR_ERP_TEST_R3_ROLLS')
BEGIN
    THROW 51200, N'Rollback target database is not approved.', 1;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.GoodsReceiptItems', N'U') IS NOT NULL
    BEGIN
        IF EXISTS (
            SELECT 1
            FROM sys.indexes
            WHERE object_id = OBJECT_ID(N'dbo.GoodsReceiptItems')
              AND name = N'UX_GoodsReceiptItems_CatalogNumber'
        )
        BEGIN
            DROP INDEX UX_GoodsReceiptItems_CatalogNumber ON dbo.GoodsReceiptItems;
        END;

        IF COL_LENGTH(N'dbo.GoodsReceiptItems', N'CatalogNumber') IS NOT NULL
        BEGIN
            ALTER TABLE dbo.GoodsReceiptItems DROP COLUMN CatalogNumber;
        END;
    END;

    IF EXISTS (
        SELECT 1
        FROM dbo.GlobalCodeAllocations WITH (UPDLOCK, HOLDLOCK)
        WHERE AllocationScope IN (N'FabricCode', N'CatalogNumber')
    )
    BEGIN
        SELECT N'ROLLBACK_PREVIEW_ONLY' AS RollbackStatus,
               @CurrentDatabase AS DatabaseName,
               N'GlobalCodeAllocations left intact to avoid reusing identities.' AS Reason;
        ROLLBACK TRANSACTION;
        RETURN;
    END;

    COMMIT TRANSACTION;
    SELECT N'ROLLBACK_APPLIED' AS RollbackStatus, @CurrentDatabase AS DatabaseName;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
