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
            AccessRole nvarchar(32) NOT NULL CONSTRAINT DF_MobileAccountCustomerAccess_AccessRole DEFAULT (N'Managed'),
            IsActive bit NOT NULL CONSTRAINT DF_MobileAccountCustomerAccess_IsActive DEFAULT ((1)),
            RelationshipType nvarchar(100) NULL,
            CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MobileAccountCustomerAccess_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
            RevokedAtUtc datetime2(3) NULL,
            CONSTRAINT PK_MobileAccountCustomerAccess PRIMARY KEY (MobileAccountCustomerAccessId),
            CONSTRAINT FK_MobileAccountCustomerAccess_MobileAccounts FOREIGN KEY (MobileAccountId) REFERENCES dbo.MobileAccounts(MobileAccountId),
            CONSTRAINT FK_MobileAccountCustomerAccess_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(CustomerID),
            CONSTRAINT UQ_MobileAccountCustomerAccess_MobileAccountId_CustomerId UNIQUE (MobileAccountId, CustomerId),
            CONSTRAINT CK_MobileAccountCustomerAccess_AccessRole CHECK (AccessRole IN (N'Primary', N'Managed')),
            CONSTRAINT CK_MobileAccountCustomerAccess_CustomerId CHECK (CustomerId > 0)
        );
    END;

    IF COL_LENGTH(N'dbo.MobileAccountCustomerAccess', N'AccessRole') IS NULL
        ALTER TABLE dbo.MobileAccountCustomerAccess ADD AccessRole nvarchar(32) NOT NULL CONSTRAINT DF_MobileAccountCustomerAccess_AccessRole DEFAULT (N'Managed');

    IF COL_LENGTH(N'dbo.MobileAccountCustomerAccess', N'IsActive') IS NULL
        ALTER TABLE dbo.MobileAccountCustomerAccess ADD IsActive bit NOT NULL CONSTRAINT DF_MobileAccountCustomerAccess_IsActive DEFAULT ((1));

    IF COL_LENGTH(N'dbo.MobileAccountCustomerAccess', N'RelationshipType') IS NULL
        ALTER TABLE dbo.MobileAccountCustomerAccess ADD RelationshipType nvarchar(100) NULL;

    IF COL_LENGTH(N'dbo.MobileAccountCustomerAccess', N'RevokedAtUtc') IS NULL
        ALTER TABLE dbo.MobileAccountCustomerAccess ADD RevokedAtUtc datetime2(3) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.MobileAccountCustomerAccess') AND name = N'CK_MobileAccountCustomerAccess_AccessRole')
        ALTER TABLE dbo.MobileAccountCustomerAccess WITH CHECK ADD CONSTRAINT CK_MobileAccountCustomerAccess_AccessRole CHECK (AccessRole IN (N'Primary', N'Managed'));

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.MobileAccountCustomerAccess') AND name = N'FK_MobileAccountCustomerAccess_MobileAccounts')
        ALTER TABLE dbo.MobileAccountCustomerAccess WITH CHECK ADD CONSTRAINT FK_MobileAccountCustomerAccess_MobileAccounts FOREIGN KEY (MobileAccountId) REFERENCES dbo.MobileAccounts(MobileAccountId);

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.MobileAccountCustomerAccess') AND name = N'FK_MobileAccountCustomerAccess_Customers')
        ALTER TABLE dbo.MobileAccountCustomerAccess WITH CHECK ADD CONSTRAINT FK_MobileAccountCustomerAccess_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(CustomerID);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.MobileAccountCustomerAccess') AND name = N'IX_MobileAccountCustomerAccess_CustomerId')
        CREATE INDEX IX_MobileAccountCustomerAccess_CustomerId ON dbo.MobileAccountCustomerAccess(CustomerId);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.MobileAccountCustomerAccess') AND name = N'IX_MobileAccountCustomerAccess_MobileAccountId')
        CREATE INDEX IX_MobileAccountCustomerAccess_MobileAccountId ON dbo.MobileAccountCustomerAccess(MobileAccountId);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.MobileAccountCustomerAccess') AND name = N'IX_MobileAccountCustomerAccess_AccessRole_IsActive')
        CREATE INDEX IX_MobileAccountCustomerAccess_AccessRole_IsActive ON dbo.MobileAccountCustomerAccess(AccessRole, IsActive, MobileAccountId, CustomerId);

    UPDATE acc
    SET AccessRole = CASE WHEN ma.CustomerId = acc.CustomerId THEN N'Primary' ELSE N'Managed' END,
        IsActive = CASE WHEN acc.IsActive IS NULL THEN 1 ELSE acc.IsActive END,
        RelationshipType = COALESCE(acc.RelationshipType, c.RelationshipType)
    FROM dbo.MobileAccountCustomerAccess acc
    JOIN dbo.MobileAccounts ma ON ma.MobileAccountId = acc.MobileAccountId
    LEFT JOIN dbo.Customers c ON c.CustomerID = acc.CustomerId
    WHERE acc.AccessRole IS NULL OR acc.AccessRole NOT IN (N'Primary', N'Managed');

    INSERT INTO dbo.MobileAccountCustomerAccess (MobileAccountId, CustomerId, AccessRole, IsActive, RelationshipType, CreatedAtUtc)
    SELECT ma.MobileAccountId, ma.CustomerId, N'Primary', 1, c.RelationshipType, SYSUTCDATETIME()
    FROM dbo.MobileAccounts ma
    JOIN dbo.Customers c ON c.CustomerID = ma.CustomerId
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
