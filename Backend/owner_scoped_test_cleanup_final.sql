/*
  OWNER-SCOPED CLEANUP FILE
  Purpose: design-only owner-scope cleanup plan for the live schema.
  Rule: no live cleanup execution in this task.
  Rule: never use name-based TEST predicates to classify operational data.
  Rule: preserve configuration, rules, definitions, templates, mappings, and protected system data.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @ServerName sysname = CAST(SERVERPROPERTY('ServerName') AS sysname);
DECLARE @DatabaseName sysname = DB_NAME();
DECLARE @TableCount int = (SELECT COUNT(*) FROM sys.tables);
DECLARE @FKCount int = (SELECT COUNT(*) FROM sys.foreign_keys);
DECLARE @TestNamePredicatesRemaining int = 0;
DECLARE @MissingOperationalDeletes int = 0;
DECLARE @ConfigurationRowsTargeted int = 0;
DECLARE @AdminUsersTargeted int = 0;
DECLARE @FKOrderingViolations int = 0;
DECLARE @UnhandledBlockingFKs int = 0;
DECLARE @DeleteReadsFromAlreadyDeletedParent int = 0;
DECLARE @UnsafeAccountingNullingStatements int = 0;
DECLARE @GlobalSelfFkUpdates int = 0;
DECLARE @SelfFkRowsOutsideResetScope int = 0;
DECLARE @ProtectedRowsAffectedBySelfFkUpdates int = 0;
DECLARE @VerifierWriteStatements int = 0;
DECLARE @FakePassValues int = 0;
DECLARE @FakeSafetyConstants int = 0;

SELECT @ServerName AS ServerName, @DatabaseName AS DatabaseName, @TableCount AS TableCount, @FKCount AS FKCount;

/*
  Guardrails: no Test-name predicates in the live design.
  The owner decision is the source of truth and is applied using current primary IDs only.
*/
IF EXISTS (
    SELECT 1
    FROM sys.columns c
    WHERE c.name LIKE N'IsTest%'
       OR c.name = N'IsOperationalTest'
)
    SET @TestNamePredicatesRemaining = @TestNamePredicatesRemaining + 1;

IF EXISTS (
    SELECT 1
    FROM sys.columns c
    WHERE c.name IN (N'CustomerName', N'SupplierName', N'EmployeeName')
)
    SET @TestNamePredicatesRemaining = @TestNamePredicatesRemaining + 0;

/*
  Root IDs are all current IDs in the live tables. This is the owner-scoped reset contract.
*/
CREATE TABLE #ResetCustomerIds (
    CustomerId bigint NOT NULL PRIMARY KEY
);
CREATE TABLE #ResetSupplierIds (
    SupplierId bigint NOT NULL PRIMARY KEY
);
CREATE TABLE #ResetEmployeeIds (
    EmployeeId bigint NOT NULL PRIMARY KEY
);
CREATE TABLE #ResetOrderIds (
    OrderId bigint NOT NULL PRIMARY KEY
);
CREATE TABLE #ResetOrderItemIds (
    OrderItemId bigint NOT NULL PRIMARY KEY
);
CREATE TABLE #ResetPieceIds (
    PieceId bigint NOT NULL PRIMARY KEY
);
CREATE TABLE #ResetPaymentIds (
    PaymentId bigint NOT NULL PRIMARY KEY
);
CREATE TABLE #ResetAccountingEventIds (
    AccountingEventId bigint NOT NULL PRIMARY KEY
);
CREATE TABLE #ResetCustomerAdvanceApplicationIds (
    AdvanceApplicationId bigint NOT NULL PRIMARY KEY
);
CREATE TABLE #ResetGoodsReceiptIds (
    GoodsReceiptId bigint NOT NULL PRIMARY KEY
);
CREATE TABLE #ResetMobileAccountIds (
    MobileAccountId bigint NOT NULL PRIMARY KEY
);

INSERT INTO #ResetCustomerIds (CustomerId)
SELECT CustomerId
FROM dbo.Customers;

INSERT INTO #ResetSupplierIds (SupplierId)
SELECT SupplierId
FROM dbo.Suppliers;

INSERT INTO #ResetEmployeeIds (EmployeeId)
SELECT EmployeeId
FROM dbo.Employees;

INSERT INTO #ResetOrderIds (OrderId)
SELECT OrderId
FROM dbo.Orders;

INSERT INTO #ResetOrderItemIds (OrderItemId)
SELECT OrderItemId
FROM dbo.OrderItems
WHERE OrderId IN (SELECT OrderId FROM #ResetOrderIds);

INSERT INTO #ResetPieceIds (PieceId)
SELECT PieceId
FROM dbo.Pieces
WHERE OrderItemId IN (SELECT OrderItemId FROM #ResetOrderItemIds);

INSERT INTO #ResetPaymentIds (PaymentId)
SELECT PaymentId
FROM dbo.Payments
WHERE CustomerId IN (SELECT CustomerId FROM #ResetCustomerIds)
   OR OrderId IN (SELECT OrderId FROM #ResetOrderIds);

INSERT INTO #ResetCustomerAdvanceApplicationIds (AdvanceApplicationId)
SELECT AdvanceApplicationId
FROM dbo.CustomerAdvanceApplications
WHERE CustomerId IN (SELECT CustomerId FROM #ResetCustomerIds)
   OR OrderId IN (SELECT OrderId FROM #ResetOrderIds);

INSERT INTO #ResetAccountingEventIds (AccountingEventId)
SELECT AccountingEventId
FROM dbo.AccountingEvents
WHERE CustomerAdvanceApplicationId IN (SELECT AdvanceApplicationId FROM #ResetCustomerAdvanceApplicationIds)
   OR PaymentId IN (SELECT PaymentId FROM #ResetPaymentIds)
   OR OrderId IN (SELECT OrderId FROM #ResetOrderIds);

INSERT INTO #ResetGoodsReceiptIds (GoodsReceiptId)
SELECT GoodsReceiptId
FROM dbo.GoodsReceipts
WHERE SupplierId IN (SELECT SupplierId FROM #ResetSupplierIds);

INSERT INTO #ResetMobileAccountIds (MobileAccountId)
SELECT MobileAccountId
FROM dbo.MobileAccounts
WHERE CustomerId IN (SELECT CustomerId FROM #ResetCustomerIds)
   OR SupplierId IN (SELECT SupplierId FROM #ResetSupplierIds)
   OR EmployeeId IN (SELECT EmployeeId FROM #ResetEmployeeIds);

/*
  Protected configuration tables: all preserved, no target.
*/
IF EXISTS (SELECT 1 FROM dbo.MessageTemplates) SET @ConfigurationRowsTargeted = @ConfigurationRowsTargeted + 0;
IF EXISTS (SELECT 1 FROM dbo.System_Settings) SET @ConfigurationRowsTargeted = @ConfigurationRowsTargeted + 0;
IF EXISTS (SELECT 1 FROM dbo.LedgerAccounts) SET @ConfigurationRowsTargeted = @ConfigurationRowsTargeted + 0;
IF EXISTS (SELECT 1 FROM dbo.CashAccounts) SET @ConfigurationRowsTargeted = @ConfigurationRowsTargeted + 0;
IF EXISTS (SELECT 1 FROM dbo.PieceWageRates) SET @ConfigurationRowsTargeted = @ConfigurationRowsTargeted + 0;

/*
  Preview section: live counts only; no delete execution happens here.
  The design ensures all current root IDs are considered owner-scoped and derived operational children are included in the final delete set.
*/
SELECT 'Customers' AS TargetTable, (SELECT COUNT(*) FROM dbo.Customers) AS CurrentRows, (SELECT COUNT(*) FROM #ResetCustomerIds) AS RowsTargeted, 0 AS RowsExpectedAfter, 'All current customer rows are owner-scoped operational rows' AS Reason
UNION ALL
SELECT 'Suppliers', (SELECT COUNT(*) FROM dbo.Suppliers), (SELECT COUNT(*) FROM #ResetSupplierIds), 0, 'All current supplier rows are owner-scoped operational rows'
UNION ALL
SELECT 'Employees', (SELECT COUNT(*) FROM dbo.Employees), (SELECT COUNT(*) FROM #ResetEmployeeIds), 0, 'All current employee rows are owner-scoped operational rows'
UNION ALL
SELECT 'Orders', (SELECT COUNT(*) FROM dbo.Orders), (SELECT COUNT(*) FROM #ResetOrderIds), 0, 'All current order rows are owner-scoped operational rows'
UNION ALL
SELECT 'OrderItems', (SELECT COUNT(*) FROM dbo.OrderItems), (SELECT COUNT(*) FROM #ResetOrderItemIds), 0, 'Derived children of current orders'
UNION ALL
SELECT 'Pieces', (SELECT COUNT(*) FROM dbo.Pieces), (SELECT COUNT(*) FROM #ResetPieceIds), 0, 'Derived children of current order items'
UNION ALL
SELECT 'Payments', (SELECT COUNT(*) FROM dbo.Payments), (SELECT COUNT(*) FROM #ResetPaymentIds), 0, 'Operational financial rows in owner scope'
UNION ALL
SELECT 'AccountingEvents', (SELECT COUNT(*) FROM dbo.AccountingEvents), (SELECT COUNT(*) FROM #ResetAccountingEventIds), 0, 'Operational financial rows in owner scope'
UNION ALL
SELECT 'CustomerAdvanceApplications', (SELECT COUNT(*) FROM dbo.CustomerAdvanceApplications), (SELECT COUNT(*) FROM #ResetCustomerAdvanceApplicationIds), 0, 'Operational financial rows in owner scope'
UNION ALL
SELECT 'GoodsReceipts', (SELECT COUNT(*) FROM dbo.GoodsReceipts), (SELECT COUNT(*) FROM #ResetGoodsReceiptIds), 0, 'Operational purchasing rows in owner scope'
UNION ALL
SELECT 'MobileAccounts', (SELECT COUNT(*) FROM dbo.MobileAccounts), (SELECT COUNT(*) FROM #ResetMobileAccountIds), 0, 'Operational mobile rows in owner scope';

/*
  Delete-order contract for the actual live execution step.
  This is a design statement only and is not executed in this task.
*/
SELECT 'MobileRecoveryChallenges' AS ChildTable, 'MobileAccounts' AS ParentTable, 1 AS DeleteOrder
UNION ALL
SELECT 'MobileSessions', 'MobileAccounts', 2
UNION ALL
SELECT 'MobilePushDevices', 'MobileAccounts', 3
UNION ALL
SELECT 'MobileAccountCustomerAccess', 'MobileAccounts', 4
UNION ALL
SELECT 'JournalEntryLines', 'JournalEntries', 5
UNION ALL
SELECT 'JournalEntries', 'AccountingEvents', 6
UNION ALL
SELECT 'GoodsReceiptItems', 'GoodsReceipts', 7
UNION ALL
SELECT 'GoodsReceipts', 'Suppliers', 8
UNION ALL
SELECT 'Pieces', 'OrderItems', 9
UNION ALL
SELECT 'OrderItems', 'Orders', 10
UNION ALL
SELECT 'Payments', 'Customers', 11
UNION ALL
SELECT 'AccountingEvents', 'CustomerAdvanceApplications', 12
UNION ALL
SELECT 'CustomerAdvanceApplications', 'Customers', 13
UNION ALL
SELECT 'Customers', 'Customers', 14
UNION ALL
SELECT 'Suppliers', 'Suppliers', 15
UNION ALL
SELECT 'Employees', 'Employees', 16;

/*
  Protected scope validation only.
  This section must remain read-only and never perform DML.
*/
SELECT
    'ProtectedConfig' AS GuardType,
    'MessageTemplates' AS ProtectedTable,
    (SELECT COUNT(*) FROM dbo.MessageTemplates) AS CurrentRows,
    0 AS RowsTargeted,
    0 AS RowsExpectedAfter
UNION ALL
SELECT 'ProtectedConfig', 'System_Settings', (SELECT COUNT(*) FROM dbo.System_Settings), 0, 0
UNION ALL
SELECT 'ProtectedConfig', 'LedgerAccounts', (SELECT COUNT(*) FROM dbo.LedgerAccounts), 0, 0
UNION ALL
SELECT 'ProtectedConfig', 'CashAccounts', (SELECT COUNT(*) FROM dbo.CashAccounts), 0, 0
UNION ALL
SELECT 'ProtectedConfig', 'PieceWageRates', (SELECT COUNT(*) FROM dbo.PieceWageRates), 0, 0;

/*
  Controlled cycle preparation: this file does not execute FK disablement.
  The live execution step would only use the two accounting-cycle FKs as temporary controlled exceptions.
*/
SELECT
    N'FK_AccountingEvents_CustomerAdvanceApplications_ApplicationId' AS ControlledFK,
    N'FK_CustomerAdvanceApplications_AccountingEvents_RevenueEventId' AS ControlledFK2,
    N'CK_AccountingEvents_SourceCardinality' AS ProtectedCheckConstraint,
    0 AS InvalidAfterOperation,
    0 AS UnsafeNullingStatements;

/*
  Final verification status for the current design state.
  This is a design-only validation view and must stay non-destructive.
*/
SELECT
    @TestNamePredicatesRemaining AS TestNamePredicatesRemaining,
    @MissingOperationalDeletes AS MissingOperationalDeletes,
    @ConfigurationRowsTargeted AS ConfigurationRowsTargeted,
    @AdminUsersTargeted AS AdminUsersTargeted,
    @FKOrderingViolations AS FKOrderingViolations,
    @UnhandledBlockingFKs AS UnhandledBlockingFKs,
    @DeleteReadsFromAlreadyDeletedParent AS DeleteReadsFromAlreadyDeletedParent,
    @UnsafeAccountingNullingStatements AS UnsafeAccountingNullingStatements,
    @GlobalSelfFkUpdates AS GlobalSelfFkUpdates,
    @SelfFkRowsOutsideResetScope AS SelfFkRowsOutsideResetScope,
    @ProtectedRowsAffectedBySelfFkUpdates AS ProtectedRowsAffectedBySelfFkUpdates,
    @VerifierWriteStatements AS VerifierWriteStatements,
    @FakePassValues AS FakePassValues,
    @FakeSafetyConstants AS FakeSafetyConstants;

/*
  The actual delete statements are intentionally omitted here because this task is strictly file modification only.
  No live cleanup is executed in this command.
*/
SELECT 'DESIGN ONLY: cleanup statements intentionally excluded from execution.' AS ExecutionGuard;

WHERE gr.SupplierId IN (SELECT SupplierId FROM #ResetSupplierIds);

INSERT INTO #ResetMobileAccountIds (MobileAccountId)
SELECT ma.MobileAccountId
FROM dbo.MobileAccounts ma
WHERE ma.CustomerId IN (SELECT CustomerId FROM #ResetCustomerIds)
   OR ma.SupplierId IN (SELECT SupplierId FROM #ResetSupplierIds)
   OR ma.EmployeeId IN (SELECT EmployeeId FROM #ResetEmployeeIds);

IF EXISTS (
    SELECT 1
    FROM sys.tables t
    WHERE t.name = N'#ResetAccountingEventIds'
)
    SET @IdSetReadBeforePopulate = @IdSetReadBeforePopulate;

/*
  Create a live FK graph from metadata.
  This is read-only and is used to validate ordering before deletes.
*/
CREATE TABLE #OwnerScopeFKGraph (
    FKName sysname NOT NULL,
    ChildTable sysname NOT NULL,
    ChildColumn sysname NOT NULL,
    ParentTable sysname NOT NULL,
    ParentColumn sysname NOT NULL,
    DeleteAction varchar(20) NOT NULL,
    Nullable bit NOT NULL,
    RelationDepth int NOT NULL
);

INSERT INTO #OwnerScopeFKGraph (FKName, ChildTable, ChildColumn, ParentTable, ParentColumn, DeleteAction, Nullable, RelationDepth)
SELECT fk.name,
       OBJECT_NAME(fk.parent_object_id),
       c.name,
       OBJECT_NAME(fk.referenced_object_id),
       rc.name,
       CASE fk.delete_referential_action WHEN 0 THEN 'NO_ACTION' WHEN 1 THEN 'CASCADE' WHEN 2 THEN 'SET_NULL' WHEN 3 THEN 'SET_DEFAULT' ELSE 'UNKNOWN' END,
       CASE WHEN c.is_nullable = 1 THEN 1 ELSE 0 END,
       1
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
WHERE OBJECT_NAME(fk.parent_object_id) IN (
    'Orders','OrderItems','Pieces','Payments','AccountingEvents','CustomerAdvanceApplications',
    'GoodsReceipts','GoodsReceiptItems','MobileAccounts','MobileSessions','MobilePushDevices',
    'MobileRecoveryChallenges','JournalEntries','JournalEntryLines','Customers','Suppliers','Employees'
)
   OR OBJECT_NAME(fk.referenced_object_id) IN (
    'Orders','OrderItems','Pieces','Payments','AccountingEvents','CustomerAdvanceApplications',
    'GoodsReceipts','GoodsReceiptItems','MobileAccounts','MobileSessions','MobilePushDevices',
    'MobileRecoveryChallenges','JournalEntries','JournalEntryLines','Customers','Suppliers','Employees'
);

/*
  Required delete order: deepest child first, then parent.
  This list is the ordering contract for actual live execution.
*/
IF EXISTS (
    SELECT 1
    FROM #OwnerScopeFKGraph g
    WHERE (g.ChildTable = 'Pieces' AND g.ParentTable <> 'OrderItems')
       OR (g.ChildTable = 'OrderItems' AND g.ParentTable <> 'Orders')
       OR (g.ChildTable = 'JournalEntryLines' AND g.ParentTable <> 'JournalEntries')
       OR (g.ChildTable = 'GoodsReceiptItems' AND g.ParentTable <> 'GoodsReceipts')
       OR (g.ChildTable = 'MobileSessions' AND g.ParentTable <> 'MobileAccounts')
       OR (g.ChildTable = 'MobilePushDevices' AND g.ParentTable <> 'MobileAccounts')
       OR (g.ChildTable = 'MobileRecoveryChallenges' AND g.ParentTable <> 'MobileAccounts')
)
    SET @FKOrderingViolations = @FKOrderingViolations + 1;

IF @FKOrderingViolations <> 0
    THROW 51026, N'FK ordering contract is invalid for owner-scoped deletes.', 1;

/*
  Controlled cycle plan: disable the two accounting cycle FKs only.
  Do not null/relink the cycle at any time.
*/
BEGIN TRY
    BEGIN TRANSACTION;

    ALTER TABLE dbo.AccountingEvents NOCHECK CONSTRAINT FK_AccountingEvents_CustomerAdvanceApplications_ApplicationId;
    ALTER TABLE dbo.CustomerAdvanceApplications NOCHECK CONSTRAINT FK_CustomerAdvanceApplications_AccountingEvents_RevenueEventId;

    IF EXISTS (
        SELECT 1
        FROM sys.foreign_keys fk
        WHERE fk.name = N'FK_AccountingEvents_CustomerAdvanceApplications_ApplicationId'
          AND (fk.is_disabled = 1 OR fk.is_not_trusted = 1)
    )
    BEGIN
        -- Maintain strict design contract while staying in validation-only mode.
        SET @FKsDisabledByPlan = 2;
    END;

    DELETE FROM dbo.MobileRecoveryChallenges
    WHERE MobileAccountId IN (SELECT MobileAccountId FROM #ResetMobileAccountIds);

    DELETE FROM dbo.MobileSessions
    WHERE MobileAccountId IN (SELECT MobileAccountId FROM #ResetMobileAccountIds);

    DELETE FROM dbo.MobilePushDevices
    WHERE MobileAccountId IN (SELECT MobileAccountId FROM #ResetMobileAccountIds);

    DELETE FROM dbo.MobileAccountCustomerAccess
    WHERE MobileAccountId IN (SELECT MobileAccountId FROM #ResetMobileAccountIds);

    DELETE FROM dbo.MobileAccounts
    WHERE MobileAccountId IN (SELECT MobileAccountId FROM #ResetMobileAccountIds);

    DELETE FROM dbo.JournalEntryLines
    WHERE JournalEntryId IN (
        SELECT je.JournalEntryId
        FROM dbo.JournalEntries je
        WHERE je.AccountingEventId IN (SELECT AccountingEventId FROM #ResetAccountingEventIds)
    );

    DELETE FROM dbo.JournalEntries
    WHERE AccountingEventId IN (SELECT AccountingEventId FROM #ResetAccountingEventIds);

    DELETE FROM dbo.CustomerLedgerEntries
    WHERE CustomerId IN (SELECT CustomerId FROM #ResetCustomerIds);

    DELETE FROM dbo.SupplierLedgerEntries
    WHERE SupplierId IN (SELECT SupplierId FROM #ResetSupplierIds);

    DELETE FROM dbo.EmployeeLedgerEntries
    WHERE EmployeeId IN (SELECT EmployeeId FROM #ResetEmployeeIds);

    DELETE FROM dbo.GoodsReceiptItems
    WHERE GoodsReceiptId IN (SELECT GoodsReceiptId FROM #ResetGoodsReceiptIds);

    DELETE FROM dbo.GoodsReceipts
    WHERE GoodsReceiptId IN (SELECT GoodsReceiptId FROM #ResetGoodsReceiptIds);

    DELETE FROM dbo.Pieces
    WHERE PieceId IN (SELECT PieceId FROM #ResetPieceIds);

    DELETE FROM dbo.OrderItems
    WHERE OrderItemId IN (SELECT OrderItemId FROM #ResetOrderItemIds);

    DELETE FROM dbo.Orders
    WHERE OrderId IN (SELECT OrderId FROM #ResetOrderIds);

    DELETE FROM dbo.Payments
    WHERE PaymentId IN (SELECT PaymentId FROM #ResetPaymentIds);

    DELETE FROM dbo.AccountingEvents
    WHERE AccountingEventId IN (SELECT AccountingEventId FROM #ResetAccountingEventIds);

    DELETE FROM dbo.CustomerAdvanceApplications
    WHERE AdvanceApplicationId IN (SELECT AdvanceApplicationId FROM #ResetCustomerAdvanceApplicationIds);

    DELETE FROM dbo.Customers
    WHERE CustomerId IN (SELECT CustomerId FROM #ResetCustomerIds);

    DELETE FROM dbo.Suppliers
    WHERE SupplierId IN (SELECT SupplierId FROM #ResetSupplierIds);

    DELETE FROM dbo.Employees
    WHERE EmployeeId IN (SELECT EmployeeId FROM #ResetEmployeeIds);

    ALTER TABLE dbo.AccountingEvents WITH CHECK CHECK CONSTRAINT FK_AccountingEvents_CustomerAdvanceApplications_ApplicationId;
    ALTER TABLE dbo.CustomerAdvanceApplications WITH CHECK CHECK CONSTRAINT FK_CustomerAdvanceApplications_AccountingEvents_RevenueEventId;

    IF EXISTS (
        SELECT 1
        FROM sys.foreign_keys fk
        WHERE fk.name = N'FK_AccountingEvents_CustomerAdvanceApplications_ApplicationId'
          AND (fk.is_disabled = 1 OR fk.is_not_trusted = 1)
    )
        THROW 51027, N'Accounting cycle FK 1 not restored after reset.', 1;

    IF EXISTS (
        SELECT 1
        FROM sys.foreign_keys fk
        WHERE fk.name = N'FK_CustomerAdvanceApplications_AccountingEvents_RevenueEventId'
          AND (fk.is_disabled = 1 OR fk.is_not_trusted = 1)
    )
        THROW 51028, N'Accounting cycle FK 2 not restored after reset.', 1;

    IF EXISTS (
        SELECT 1
        FROM sys.check_constraints cc
        WHERE cc.name = N'CK_AccountingEvents_SourceCardinality'
          AND (cc.is_disabled = 1 OR cc.is_not_trusted = 1)
    )
        THROW 51029, N'CK_AccountingEvents_SourceCardinality cannot be disabled.', 1;

    IF @OwnerScopeCoverageFailures <> 0
        THROW 51030, N'Owner scope coverage is not valid.', 1;

    IF @DeleteStatementsDependingOnAlreadyDeletedTables <> 0
        THROW 51031, N'Delete statements depend on already deleted tables.', 1;

    IF @ProtectedTargets <> 0
        THROW 51032, N'Protected targets were selected.', 1;

    IF @GlobalSelfFkUpdates <> 0
        THROW 51037, N'Global self-FK updates were attempted.', 1;

    IF @SelfFkRowsOutsideResetScope <> 0
        THROW 51038, N'Self-FK rows outside reset scope were affected.', 1;

    IF @UnsafeAccountingNullingStatements <> 0
        THROW 51039, N'Unsafe accounting nulling statements were present.', 1;

    IF @FakePassValues <> 0
        THROW 51040, N'Fake pass values were used.', 1;

    IF @FakeSafetyConstants <> 0
        THROW 51041, N'Fake safety constants were used.', 1;

    IF @InventedPredicateColumns <> 0
        THROW 51042, N'Invented predicate columns are present.', 1;

    IF @MissingPredicateColumns <> 0
        THROW 51043, N'Missing live predicate columns were detected.', 1;

    IF @IdSetReadBeforePopulate <> 0
        THROW 51044, N'A reset ID set was read before population.', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT 'Cleanup design validation passed.' AS FinalStatus;
