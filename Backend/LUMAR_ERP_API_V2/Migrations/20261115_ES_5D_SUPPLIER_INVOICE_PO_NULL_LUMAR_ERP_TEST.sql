SET XACT_ABORT ON;
IF DB_NAME() <> N'LUMAR_ERP_TEST' THROW 52320,N'ES-5D is restricted to LUMAR_ERP_TEST only.',1;
BEGIN TRY BEGIN TRANSACTION;
IF COL_LENGTH(N'dbo.SupplierInvoices',N'PurchaseOrderId') IS NULL THROW 52321,N'SupplierInvoices.PurchaseOrderId is required.',1;
IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.SupplierInvoices') AND name = N'PurchaseOrderId' AND is_nullable = 0)
BEGIN
    ALTER TABLE dbo.SupplierInvoices ALTER COLUMN PurchaseOrderId int NULL;
END;
COMMIT TRANSACTION; END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
