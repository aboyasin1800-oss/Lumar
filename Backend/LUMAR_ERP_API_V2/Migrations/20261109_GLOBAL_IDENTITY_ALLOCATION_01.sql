SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;

IF DB_NAME() NOT IN (N'LUMAR_ERP', N'LUMAR_ERP_TEST_R3_ROLLS')
    THROW 51700, 'Global identity allocation migration: database is not approved.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.GlobalCodeAllocations', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.GlobalCodeAllocations (
            AllocationScope nvarchar(100) NOT NULL PRIMARY KEY,
            Prefix nvarchar(30) NOT NULL,
            CurrentValue bigint NOT NULL CONSTRAINT DF_GlobalCodeAllocations_CurrentValue DEFAULT (0),
            CreatedAt datetime2(3) NOT NULL CONSTRAINT DF_GlobalCodeAllocations_CreatedAt DEFAULT (SYSUTCDATETIME()),
            UpdatedAt datetime2(3) NOT NULL CONSTRAINT DF_GlobalCodeAllocations_UpdatedAt DEFAULT (SYSUTCDATETIME())
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.GlobalCodeAllocations WITH (UPDLOCK,HOLDLOCK) WHERE AllocationScope = N'CatalogNumber')
        INSERT INTO dbo.GlobalCodeAllocations (AllocationScope, Prefix, CurrentValue, CreatedAt, UpdatedAt)
        VALUES (N'CatalogNumber', N'CAT', 0, SYSUTCDATETIME(), SYSUTCDATETIME());

    IF NOT EXISTS (SELECT 1 FROM dbo.GlobalCodeAllocations WITH (UPDLOCK,HOLDLOCK) WHERE AllocationScope = N'FabricCode')
        INSERT INTO dbo.GlobalCodeAllocations (AllocationScope, Prefix, CurrentValue, CreatedAt, UpdatedAt)
        VALUES (N'FabricCode', N'FA', 0, SYSUTCDATETIME(), SYSUTCDATETIME());

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
