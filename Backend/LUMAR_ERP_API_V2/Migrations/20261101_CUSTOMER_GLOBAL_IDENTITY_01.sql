SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;

IF DB_NAME() NOT IN (N'LUMAR_ERP_ES_VALIDATION', N'LUMAR_CUSTOMER_IDENTITY_TEST_20261101')
    THROW 51700, 'Customer identity migration: database is not approved.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    SELECT CustomerID, CustomerCode, ParentCustomerId
    INTO #CustomerIdentityBefore
    FROM dbo.Customers WITH (TABLOCKX, HOLDLOCK);

    IF COL_LENGTH(N'dbo.Customers', N'GlobalCustomerUid') IS NULL
        ALTER TABLE dbo.Customers ADD GlobalCustomerUid uniqueidentifier NULL;

    EXEC(N'UPDATE dbo.Customers SET GlobalCustomerUid = NEWID() WHERE GlobalCustomerUid IS NULL;
        IF EXISTS (SELECT 1 FROM dbo.Customers WHERE GlobalCustomerUid IS NULL)
            THROW 51701, ''Null customer UID remains.'', 1;
        IF EXISTS (SELECT GlobalCustomerUid FROM dbo.Customers GROUP BY GlobalCustomerUid HAVING COUNT(*) > 1)
            THROW 51702, ''Duplicate customer UID exists.'', 1;
        ALTER TABLE dbo.Customers ALTER COLUMN GlobalCustomerUid uniqueidentifier NOT NULL;');

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Customers') AND name = N'UQ_Customers_GlobalCustomerUid')
        EXEC(N'ALTER TABLE dbo.Customers ADD CONSTRAINT UQ_Customers_GlobalCustomerUid UNIQUE NONCLUSTERED (GlobalCustomerUid);');

    IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.Customers') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'dbo.Customers'), N'GlobalCustomerUid', 'ColumnId'))
        EXEC(N'ALTER TABLE dbo.Customers ADD CONSTRAINT DF_Customers_GlobalCustomerUid DEFAULT (NEWID()) FOR GlobalCustomerUid;');

    IF (SELECT COUNT_BIG(*) FROM #CustomerIdentityBefore) <> (SELECT COUNT_BIG(*) FROM dbo.Customers)
        THROW 51703, 'Customer count changed during migration.', 1;
    IF EXISTS (
        SELECT CustomerID, CONVERT(varbinary(40), CustomerCode), ParentCustomerId FROM #CustomerIdentityBefore
        EXCEPT
        SELECT CustomerID, CONVERT(varbinary(40), CustomerCode), ParentCustomerId FROM dbo.Customers)
        THROW 51704, 'Existing customer identity or parent changed.', 1;

    DROP TABLE #CustomerIdentityBefore;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;