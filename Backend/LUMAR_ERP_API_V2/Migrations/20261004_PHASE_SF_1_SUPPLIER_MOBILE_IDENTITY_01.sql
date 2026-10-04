SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

IF DB_NAME() <> N'LUMAR_ERP_ES_VALIDATION'
    THROW 51600, N'This Phase SF-1 supplier mobile identity migration is restricted to LUMAR_ERP_ES_VALIDATION.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.MobileAccounts', N'U') IS NULL
        THROW 51601, N'MobileAccounts is required for the Supplier mobile identity phase.', 1;

    IF OBJECT_ID(N'dbo.Suppliers', N'U') IS NULL
        THROW 51602, N'Suppliers is required to bind SupplierId in the mobile identity phase.', 1;

    IF COL_LENGTH(N'dbo.MobileAccounts', N'SupplierId') IS NULL
        ALTER TABLE dbo.MobileAccounts ADD SupplierId int NULL;

    DECLARE @invalidSupplierOwners int = 0;
    DECLARE @unsupportedAccountTypes int = 0;
    DECLARE @multiOwnerViolation int = 0;
    DECLARE @customerSupplierMismatch int = 0;
    DECLARE @employeeSupplierMismatch int = 0;

    EXEC sys.sp_executesql
        N'
            SELECT @invalidSupplierOwners = CASE WHEN EXISTS (SELECT 1 FROM dbo.MobileAccounts ma WHERE ma.AccountType = N''Supplier'' AND ma.SupplierId IS NULL) THEN 1 ELSE 0 END,
                   @unsupportedAccountTypes = CASE WHEN EXISTS (SELECT 1 FROM dbo.MobileAccounts ma WHERE ma.AccountType NOT IN (N''Customer'', N''Employee'', N''Supplier'')) THEN 1 ELSE 0 END,
                   @multiOwnerViolation = CASE WHEN EXISTS (SELECT 1 FROM dbo.MobileAccounts ma WHERE (ma.CustomerId IS NOT NULL AND ma.EmployeeId IS NOT NULL) OR (ma.CustomerId IS NOT NULL AND ma.SupplierId IS NOT NULL) OR (ma.EmployeeId IS NOT NULL AND ma.SupplierId IS NOT NULL)) THEN 1 ELSE 0 END,
                   @customerSupplierMismatch = CASE WHEN EXISTS (SELECT 1 FROM dbo.MobileAccounts ma WHERE ma.AccountType = N''Customer'' AND ma.SupplierId IS NOT NULL) THEN 1 ELSE 0 END,
                   @employeeSupplierMismatch = CASE WHEN EXISTS (SELECT 1 FROM dbo.MobileAccounts ma WHERE ma.AccountType = N''Employee'' AND ma.SupplierId IS NOT NULL) THEN 1 ELSE 0 END;
        ',
        N'@invalidSupplierOwners int OUTPUT, @unsupportedAccountTypes int OUTPUT, @multiOwnerViolation int OUTPUT, @customerSupplierMismatch int OUTPUT, @employeeSupplierMismatch int OUTPUT',
        @invalidSupplierOwners = @invalidSupplierOwners OUTPUT,
        @unsupportedAccountTypes = @unsupportedAccountTypes OUTPUT,
        @multiOwnerViolation = @multiOwnerViolation OUTPUT,
        @customerSupplierMismatch = @customerSupplierMismatch OUTPUT,
        @employeeSupplierMismatch = @employeeSupplierMismatch OUTPUT;

    IF @invalidSupplierOwners = 1 OR @unsupportedAccountTypes = 1 OR @multiOwnerViolation = 1 OR @customerSupplierMismatch = 1 OR @employeeSupplierMismatch = 1
        THROW 51603, N'Existing MobileAccounts data conflicts with the supplier ownership contract; no backfill or mutation is allowed in this phase.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_MobileAccounts_Suppliers_SupplierId'
    )
        ALTER TABLE dbo.MobileAccounts DROP CONSTRAINT FK_MobileAccounts_Suppliers_SupplierId;

    IF EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.MobileAccounts')
          AND name = N'CK_MobileAccounts_AccountType'
    )
        ALTER TABLE dbo.MobileAccounts DROP CONSTRAINT CK_MobileAccounts_AccountType;

    IF EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.MobileAccounts')
          AND name = N'CK_MobileAccounts_AccountLink'
    )
        ALTER TABLE dbo.MobileAccounts DROP CONSTRAINT CK_MobileAccounts_AccountLink;

    EXEC(N'
        ALTER TABLE dbo.MobileAccounts WITH CHECK
            ADD CONSTRAINT CK_MobileAccounts_AccountType
            CHECK (AccountType IN (N''Customer'', N''Employee'', N''Supplier''));

        ALTER TABLE dbo.MobileAccounts WITH CHECK
            ADD CONSTRAINT CK_MobileAccounts_AccountLink
            CHECK
            (
                (AccountType = N''Customer'' AND CustomerId IS NOT NULL AND EmployeeId IS NULL AND SupplierId IS NULL)
                OR (AccountType = N''Employee'' AND EmployeeId IS NOT NULL AND CustomerId IS NULL AND SupplierId IS NULL)
                OR (AccountType = N''Supplier'' AND SupplierId IS NOT NULL AND CustomerId IS NULL AND EmployeeId IS NULL)
            );

        ALTER TABLE dbo.MobileAccounts WITH CHECK
            ADD CONSTRAINT FK_MobileAccounts_Suppliers_SupplierId
            FOREIGN KEY (SupplierId) REFERENCES dbo.Suppliers(SupplierId);
    ');

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.MobileAccounts')
          AND name = N'IX_MobileAccounts_SupplierId'
    )
    BEGIN
        EXEC(N'
            CREATE UNIQUE INDEX IX_MobileAccounts_SupplierId
                ON dbo.MobileAccounts(SupplierId)
                WHERE SupplierId IS NOT NULL;
        ');
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
