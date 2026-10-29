SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'LUMAR_ERP_ES_VALIDATION'
    THROW 53001, N'This schema correction is restricted to LUMAR_ERP_ES_VALIDATION.', 1;

IF OBJECT_ID(N'dbo.EmployeePieceRateAssignments', N'U') IS NULL
    THROW 53002, N'dbo.EmployeePieceRateAssignments is required for this constraint correction.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.EmployeePieceRateAssignments')
          AND name = N'CK_EmployeePieceRateAssignments_Rate'
    )
        ALTER TABLE dbo.EmployeePieceRateAssignments DROP CONSTRAINT CK_EmployeePieceRateAssignments_Rate;

    ALTER TABLE dbo.EmployeePieceRateAssignments
        WITH CHECK
        ADD CONSTRAINT CK_EmployeePieceRateAssignments_Rate CHECK (Rate >= 0);

    ALTER TABLE dbo.EmployeePieceRateAssignments
        WITH CHECK
        CHECK CONSTRAINT CK_EmployeePieceRateAssignments_Rate;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
