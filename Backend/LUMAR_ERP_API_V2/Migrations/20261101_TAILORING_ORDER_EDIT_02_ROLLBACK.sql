SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
SET NOCOUNT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.TailoringPieceCancellations', N'U') IS NOT NULL
        DROP TABLE dbo.TailoringPieceCancellations;

    IF OBJECT_ID(N'dbo.TailoringPieceAddOperations', N'U') IS NOT NULL
        DROP TABLE dbo.TailoringPieceAddOperations;

    IF OBJECT_ID(N'dbo.TailoringOrderEditOperations', N'U') IS NOT NULL
        DROP TABLE dbo.TailoringOrderEditOperations;

    IF OBJECT_ID(N'dbo.TailoringPieceBenefitBalances', N'U') IS NOT NULL
        DROP TABLE dbo.TailoringPieceBenefitBalances;

    IF COL_LENGTH(N'dbo.TailoringPieceSaleValues', N'BenefitsCaptured') IS NOT NULL
    BEGIN
        DECLARE @benefitsConstraint sysname = (
            SELECT name
            FROM sys.default_constraints
            WHERE parent_object_id = OBJECT_ID(N'dbo.TailoringPieceSaleValues')
              AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'dbo.TailoringPieceSaleValues'), N'BenefitsCaptured', 'ColumnId')
        );
        IF @benefitsConstraint IS NOT NULL
            EXEC(N'SET QUOTED_IDENTIFIER ON; ALTER TABLE dbo.TailoringPieceSaleValues DROP CONSTRAINT ' + QUOTENAME(@benefitsConstraint));
        ALTER TABLE dbo.TailoringPieceSaleValues DROP COLUMN BenefitsCaptured;
    END;

    IF COL_LENGTH(N'dbo.TailoringPieceSaleValues', N'OriginalBasePoints') IS NOT NULL
        ALTER TABLE dbo.TailoringPieceSaleValues DROP COLUMN OriginalBasePoints;

    IF COL_LENGTH(N'dbo.Orders', N'EditVersion') IS NOT NULL
        ALTER TABLE dbo.Orders DROP COLUMN EditVersion;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
