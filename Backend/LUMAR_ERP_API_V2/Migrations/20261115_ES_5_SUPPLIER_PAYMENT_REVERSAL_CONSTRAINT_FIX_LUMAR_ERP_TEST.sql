SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

IF DB_NAME() <> N'LUMAR_ERP_TEST'
    THROW 52360, N'This fix is restricted to LUMAR_ERP_TEST only.', 1;

IF EXISTS (SELECT 1 FROM dbo.SupplierFinancialPayments WHERE OriginalSupplierPaymentId IS NOT NULL GROUP BY OriginalSupplierPaymentId HAVING COUNT(*) > 1)
    THROW 52361, N'Duplicate supplier payment reversals exist; correction is refused.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.SupplierFinancialPayments') AND name = N'UQ_SupplierFinancialPayments_OriginalReversal')
        ALTER TABLE dbo.SupplierFinancialPayments DROP CONSTRAINT UQ_SupplierFinancialPayments_OriginalReversal;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.SupplierFinancialPayments') AND name = N'UX_SupplierFinancialPayments_OriginalReversal')
        CREATE UNIQUE INDEX UX_SupplierFinancialPayments_OriginalReversal ON dbo.SupplierFinancialPayments(OriginalSupplierPaymentId) WHERE OriginalSupplierPaymentId IS NOT NULL;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
