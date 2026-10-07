SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.MobileAccounts', N'U') IS NULL
        THROW 51600, N'MobileAccounts table is required for the mobile identity repair.', 1;

    IF COL_LENGTH(N'dbo.MobileAccounts', N'UserId') IS NULL
        EXEC(N'ALTER TABLE dbo.MobileAccounts ADD UserId int NULL;');

    IF COL_LENGTH(N'dbo.MobileAccounts', N'SupplierId') IS NULL
        EXEC(N'ALTER TABLE dbo.MobileAccounts ADD SupplierId int NULL;');

    IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.MobileAccounts') AND name = N'FK_MobileAccounts_Users_UserId')
            EXEC(N'ALTER TABLE dbo.MobileAccounts WITH CHECK ADD CONSTRAINT FK_MobileAccounts_Users_UserId FOREIGN KEY (UserId) REFERENCES dbo.Users(UserID);');
    END;

    IF OBJECT_ID(N'dbo.Suppliers', N'U') IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.MobileAccounts') AND name = N'FK_MobileAccounts_Suppliers_SupplierId')
            EXEC(N'ALTER TABLE dbo.MobileAccounts WITH CHECK ADD CONSTRAINT FK_MobileAccounts_Suppliers_SupplierId FOREIGN KEY (SupplierId) REFERENCES dbo.Suppliers(SupplierId);');
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.MobileAccounts') AND name = N'IX_MobileAccounts_UserId')
        EXEC(N'CREATE UNIQUE INDEX IX_MobileAccounts_UserId ON dbo.MobileAccounts(UserId) WHERE UserId IS NOT NULL;');

    IF OBJECT_ID(N'dbo.Suppliers', N'U') IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.MobileAccounts') AND name = N'IX_MobileAccounts_SupplierId')
            EXEC(N'CREATE UNIQUE INDEX IX_MobileAccounts_SupplierId ON dbo.MobileAccounts(SupplierId) WHERE SupplierId IS NOT NULL;');
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.MobileAccounts') AND name = N'CK_MobileAccounts_AccountType')
        EXEC(N'ALTER TABLE dbo.MobileAccounts WITH CHECK ADD CONSTRAINT CK_MobileAccounts_AccountType CHECK (AccountType IN (N''Customer'', N''Employee'', N''Supplier''));');

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.MobileAccounts') AND name = N'CK_MobileAccounts_AccountLink')
        EXEC(N'ALTER TABLE dbo.MobileAccounts WITH CHECK ADD CONSTRAINT CK_MobileAccounts_AccountLink CHECK ((AccountType = N''Customer'' AND CustomerId IS NOT NULL AND EmployeeId IS NULL AND SupplierId IS NULL) OR (AccountType = N''Employee'' AND EmployeeId IS NOT NULL AND CustomerId IS NULL AND SupplierId IS NULL) OR (AccountType = N''Supplier'' AND SupplierId IS NOT NULL AND CustomerId IS NULL AND EmployeeId IS NULL));');

    DECLARE @userId int = (SELECT TOP 1 UserID FROM dbo.Users WHERE Username = N'viewer_step27' AND IsActive = 1 ORDER BY UserID);
    DECLARE @employeeId int = (SELECT TOP 1 EmployeeID FROM dbo.Employees WHERE IsActive = 1 ORDER BY EmployeeID DESC);

    IF @userId IS NOT NULL AND @employeeId IS NOT NULL
    BEGIN
        IF COL_LENGTH(N'dbo.MobileAccounts', N'UserId') IS NOT NULL
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM dbo.MobileAccounts WHERE UserId = @userId)
            BEGIN
                INSERT INTO dbo.MobileAccounts (
                    AccountType,
                    CustomerId,
                    EmployeeId,
                    SupplierId,
                    UserId,
                    Username,
                    NormalizedUsername,
                    PasswordHash,
                    IsActive,
                    FailedLoginAttempts,
                    LockoutEndUtc,
                    SecurityStamp,
                    TokenVersion,
                    CreatedAtUtc,
                    UpdatedAtUtc)
                VALUES (
                    N'Employee',
                    NULL,
                    @employeeId,
                    NULL,
                    @userId,
                    N'viewer_step27',
                    N'VIEWER_STEP27',
                    N'',
                    1,
                    0,
                    NULL,
                    CAST(NEWID() AS nvarchar(128)),
                    0,
                    SYSUTCDATETIME(),
                    NULL);
            END;
        END;
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
