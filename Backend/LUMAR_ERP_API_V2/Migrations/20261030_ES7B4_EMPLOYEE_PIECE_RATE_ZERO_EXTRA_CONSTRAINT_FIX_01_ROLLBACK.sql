SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'LUMAR_ERP_ES_VALIDATION'
    THROW 53010, N'This rollback is restricted to LUMAR_ERP_ES_VALIDATION.', 1;

IF EXISTS
(
    SELECT 1
    FROM dbo.EmployeePieceRateAssignments
    WHERE Rate = 0
)
    THROW 53011, N'Rollback is refused because zero-rate assignments exist; legacy constraint cannot be restored safely.', 1;

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
        ADD CONSTRAINT CK_EmployeePieceRateAssignments_Rate CHECK (Rate > 0);

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
