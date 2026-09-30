SET XACT_ABORT ON;
IF DB_NAME()<>N'LUMAR_ERP_ES_VALIDATION' THROW 52530,N'ES-7B-3 receipt confirmation setup is restricted to LUMAR_ERP_ES_VALIDATION.',1;
BEGIN TRY
    BEGIN TRANSACTION;
    IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE name=N'CK_GoodsReceipts_ES6_Status') ALTER TABLE dbo.GoodsReceipts DROP CONSTRAINT CK_GoodsReceipts_ES6_Status;
    IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE name=N'CK_GoodsReceiptItems_ES6_Status') ALTER TABLE dbo.GoodsReceiptItems DROP CONSTRAINT CK_GoodsReceiptItems_ES6_Status;
    IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name=N'CK_GoodsReceipts_ES7_Status') ALTER TABLE dbo.GoodsReceipts ADD CONSTRAINT CK_GoodsReceipts_ES7_Status CHECK(ReceiptStatus IN(N'Posted',N'Confirmed',N'Reversed'));
    IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name=N'CK_GoodsReceiptItems_ES7_Status') ALTER TABLE dbo.GoodsReceiptItems ADD CONSTRAINT CK_GoodsReceiptItems_ES7_Status CHECK(LineStatus IS NULL OR LineStatus IN(N'Draft',N'Confirmed',N'Posted',N'Reversed'));
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
