SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'LUMAR_ERP_ES_VALIDATION'
    THROW 52210, N'This ES-4 correction is restricted to LUMAR_ERP_ES_VALIDATION.', 1;

IF OBJECT_ID(N'dbo.EmployeePayments', N'U') IS NULL
    THROW 52211, N'EmployeePayments is required.', 1;

IF EXISTS
(
    SELECT 1
    FROM dbo.EmployeePayments
    WHERE OriginalPaymentId IS NOT NULL
    GROUP BY OriginalPaymentId
    HAVING COUNT(*) > 1
)
    THROW 52212, N'Duplicate payment reversals exist; correction is refused.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF EXISTS
    (
        SELECT 1
        FROM sys.key_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.EmployeePayments')
          AND name = N'UQ_EmployeePayments_OriginalReversal'
    )
        ALTER TABLE dbo.EmployeePayments DROP CONSTRAINT UQ_EmployeePayments_OriginalReversal;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.EmployeePayments')
          AND name = N'UX_EmployeePayments_OriginalReversal'
    )
        CREATE UNIQUE INDEX UX_EmployeePayments_OriginalReversal
            ON dbo.EmployeePayments(OriginalPaymentId)
            WHERE OriginalPaymentId IS NOT NULL;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;