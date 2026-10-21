SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.MobileAccounts', N'U') IS NULL
        THROW 51600, N'MobileAccounts table is required for family customer access foundation.', 1;

    IF OBJECT_ID(N'dbo.Customers', N'U') IS NULL
        THROW 51601, N'Customers table is required for family customer access foundation.', 1;

    IF OBJECT_ID(N'dbo.MobileAccountCustomerAccess', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.MobileAccountCustomerAccess (
            MobileAccountCustomerAccessId int IDENTITY(1,1) NOT NULL,
            MobileAccountId int NOT NULL,
            CustomerId int NOT NULL,
            CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MobileAccountCustomerAccess_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
            CONSTRAINT PK_MobileAccountCustomerAccess PRIMARY KEY (MobileAccountCustomerAccessId),
            CONSTRAINT FK_MobileAccountCustomerAccess_MobileAccounts FOREIGN KEY (MobileAccountId) REFERENCES dbo.MobileAccounts(MobileAccountId),
            CONSTRAINT FK_MobileAccountCustomerAccess_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(CustomerID),
            CONSTRAINT UQ_MobileAccountCustomerAccess_MobileAccountId_CustomerId UNIQUE (MobileAccountId, CustomerId),
            CONSTRAINT CK_MobileAccountCustomerAccess_CustomerId CHECK (CustomerId > 0)
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.MobileAccountCustomerAccess') AND name = N'FK_MobileAccountCustomerAccess_MobileAccounts')
        ALTER TABLE dbo.MobileAccountCustomerAccess WITH CHECK ADD CONSTRAINT FK_MobileAccountCustomerAccess_MobileAccounts FOREIGN KEY (MobileAccountId) REFERENCES dbo.MobileAccounts(MobileAccountId);

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.MobileAccountCustomerAccess') AND name = N'FK_MobileAccountCustomerAccess_Customers')
        ALTER TABLE dbo.MobileAccountCustomerAccess WITH CHECK ADD CONSTRAINT FK_MobileAccountCustomerAccess_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(CustomerID);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.MobileAccountCustomerAccess') AND name = N'IX_MobileAccountCustomerAccess_CustomerId')
        CREATE INDEX IX_MobileAccountCustomerAccess_CustomerId ON dbo.MobileAccountCustomerAccess(CustomerId);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.MobileAccountCustomerAccess') AND name = N'IX_MobileAccountCustomerAccess_MobileAccountId')
        CREATE INDEX IX_MobileAccountCustomerAccess_MobileAccountId ON dbo.MobileAccountCustomerAccess(MobileAccountId);

    INSERT INTO dbo.MobileAccountCustomerAccess (MobileAccountId, CustomerId, CreatedAtUtc)
    SELECT ma.MobileAccountId, ma.CustomerId, SYSUTCDATETIME()
    FROM dbo.MobileAccounts ma
    WHERE ma.AccountType = N'Customer'
      AND ma.CustomerId IS NOT NULL
      AND ma.CustomerId > 0
      AND NOT EXISTS (
          SELECT 1
          FROM dbo.MobileAccountCustomerAccess acc
          WHERE acc.MobileAccountId = ma.MobileAccountId
            AND acc.CustomerId = ma.CustomerId
      );

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
