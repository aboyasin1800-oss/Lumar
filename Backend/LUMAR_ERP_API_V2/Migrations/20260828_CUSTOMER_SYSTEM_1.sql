SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH('dbo.Customers', 'Address') IS NULL
    ALTER TABLE dbo.Customers ADD Address nvarchar(250) NULL;

IF COL_LENGTH('dbo.Customers', 'Notes') IS NULL
    ALTER TABLE dbo.Customers ADD Notes nvarchar(1000) NULL;

IF COL_LENGTH('dbo.Customers', 'IsActive') IS NULL
BEGIN
    ALTER TABLE dbo.Customers ADD IsActive bit NOT NULL CONSTRAINT DF_Customers_IsActive DEFAULT (1);
END;

IF COL_LENGTH('dbo.Customers', 'ParentCustomerId') IS NULL
    ALTER TABLE dbo.Customers ADD ParentCustomerId int NULL;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Customers_ParentCustomer')
    ALTER TABLE dbo.Customers ADD CONSTRAINT FK_Customers_ParentCustomer FOREIGN KEY (ParentCustomerId) REFERENCES dbo.Customers(CustomerID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Customers') AND name = 'IX_Customers_ParentCustomerId')
    CREATE INDEX IX_Customers_ParentCustomerId ON dbo.Customers(ParentCustomerId);

COMMIT TRANSACTION;