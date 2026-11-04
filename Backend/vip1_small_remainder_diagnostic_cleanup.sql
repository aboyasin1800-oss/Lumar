/*
  VIP-1 SMALL REMAINDER DIAGNOSTIC CLEANUP

  Scope:
    - approved residual list only
    - no global NOCHECK CONSTRAINT ALL
    - no DISABLE TRIGGER ALL
    - no commit
    - rollback is mandatory
    - no outside-scope delete

  Required path in this run:
    1. Check the extra child tables that are adjacent to GoodsReceiptItems.
    2. If any of them has rows, stop and display the exact table name / row count / relationship.
    3. Delete in approved child-first order, with the accounting cycle disabled only for the three exact constraints.
    4. Re-enable the three exact constraints with WITH CHECK before rollback.
    5. Roll back without COMMIT.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

DECLARE @DeleteZero bit = 0;
DECLARE @ConstraintsReenabledAndTrusted bit = 0;
DECLARE @RollbackVerified bit = 0;
DECLARE @DisabledFKCount int = 0;
DECLARE @DisabledCheckCount int = 0;
DECLARE @UntrustedFKCount int = 0;
DECLARE @UntrustedCheckCount int = 0;
DECLARE @ExactConstraintList nvarchar(4000) = N'FK_CustomerAdvanceApplications_AccountingEvents_RevenueEventId;FK_AccountingEvents_CustomerAdvanceApplications_ApplicationId;CK_AccountingEvents_SourceCardinality';

CREATE TABLE #TargetTableList (
    TableName sysname NOT NULL PRIMARY KEY,
    DeleteOrder int NOT NULL
);

INSERT INTO #TargetTableList (TableName, DeleteOrder)
VALUES
    (N'OrderItemFabrics', 1),
    (N'OrderItems', 2),
    (N'Orders', 3),
    (N'Pieces', 4),
    (N'PurchaseOrderItems', 5),
    (N'PurchaseOrders', 6),
    (N'Payments', 7),
    (N'TrackingEvents', 8),
    (N'AccountingEvents', 9),
    (N'CustomerAdvanceApplications', 10),
    (N'InventoryReceiptPostings', 11),
    (N'GoodsReceiptItems', 12),
    (N'SupplierInvoiceLines', 13),
    (N'SupplierInvoices', 14),
    (N'Suppliers', 15),
    (N'GoodsReceipts', 16),
    (N'FabricRolls', 17),
    (N'UserSessions', 18);

CREATE TABLE #ProtectedTableList (
    TableName sysname NOT NULL PRIMARY KEY
);

INSERT INTO #ProtectedTableList (TableName)
VALUES
    (N'Users'),
    (N'SecurityRoles'),
    (N'SecurityPermissions'),
    (N'RolePermissionMappings'),
    (N'System_Settings'),
    (N'LedgerAccounts'),
    (N'CashAccounts'),
    (N'MessageTemplates'),
    (N'PieceWageRates');

IF EXISTS (
    SELECT 1
    FROM #TargetTableList t
    INNER JOIN #ProtectedTableList p ON p.TableName = t.TableName
) BEGIN
    THROW 50001, N'OUTSIDE_SCOPE_DELETE_ATTEMPTED: protected table is included in the target list.', 1;
END;

IF EXISTS (
    SELECT t.TableName
    FROM #TargetTableList t
    LEFT JOIN sys.tables st ON st.name = t.TableName
    WHERE st.object_id IS NULL
) BEGIN
    THROW 50002, N'TARGET_TABLE_NOT_FOUND: an approved residual table is missing from the current schema.', 1;
END;

CREATE TABLE #BeforeCounts (
    TableName sysname NOT NULL PRIMARY KEY,
    RowsCount bigint NOT NULL
);

INSERT INTO #BeforeCounts (TableName, RowsCount)
SELECT t.TableName,
       (SELECT SUM(p.rows)
        FROM sys.partitions p
        WHERE p.object_id = OBJECT_ID(N'dbo.' + t.TableName)
          AND p.index_id IN (0,1))
FROM #TargetTableList t;

SELECT 'BEFORE_COUNTS' AS Phase, TableName, RowsCount
FROM #BeforeCounts
ORDER BY TableName;

CREATE TABLE #AdjacentChildCheck (
    TableName sysname NOT NULL PRIMARY KEY,
    RowsCount bigint NOT NULL,
    RelationshipDescription nvarchar(400) NOT NULL
);

INSERT INTO #AdjacentChildCheck (TableName, RowsCount, RelationshipDescription)
SELECT N'GoodsReceiptDifferences', (SELECT COUNT(*) FROM dbo.GoodsReceiptDifferences), N'GoodsReceiptDifferences -> GoodsReceiptItems (FK_GoodsReceiptDifferences_Item)'
UNION ALL
SELECT N'GoodsReceiptReversalLines', (SELECT COUNT(*) FROM dbo.GoodsReceiptReversalLines), N'GoodsReceiptReversalLines -> GoodsReceiptItems (FK_GoodsReceiptReversalLines_Item)'
UNION ALL
SELECT N'GoodsReceiptItemStorageAllocations', (SELECT COUNT(*) FROM dbo.GoodsReceiptItemStorageAllocations), N'GoodsReceiptItemStorageAllocations -> GoodsReceiptItems (FK_GoodsReceiptItemStorageAllocations_Item)';

SELECT 'ADJACENT_CHILD_CHECK' AS Phase, TableName, RowsCount, RelationshipDescription
FROM #AdjacentChildCheck
ORDER BY TableName;

IF EXISTS (
    SELECT 1
    FROM #AdjacentChildCheck
    WHERE RowsCount > 0
) BEGIN
    SELECT 'STOP_OUTSIDE_APPROVED_CHILD_DATA' AS Phase,
           TableName,
           RowsCount,
           RelationshipDescription
    FROM #AdjacentChildCheck
    WHERE RowsCount > 0;

    THROW 50003, N'MANUAL_OWNER_DECISION_REQUIRED: one or more adjacent child tables has rows and is outside the current closed delete list.', 1;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    ALTER TABLE dbo.CustomerAdvanceApplications NOCHECK CONSTRAINT FK_CustomerAdvanceApplications_AccountingEvents_RevenueEventId;
    ALTER TABLE dbo.AccountingEvents NOCHECK CONSTRAINT FK_AccountingEvents_CustomerAdvanceApplications_ApplicationId;
    ALTER TABLE dbo.AccountingEvents NOCHECK CONSTRAINT CK_AccountingEvents_SourceCardinality;

    SELECT
        (SELECT COUNT(*)
         FROM sys.foreign_keys
         WHERE name IN (N'FK_CustomerAdvanceApplications_AccountingEvents_RevenueEventId', N'FK_AccountingEvents_CustomerAdvanceApplications_ApplicationId')
           AND is_disabled = 1) AS DisabledFKCount,
        (SELECT COUNT(*)
         FROM sys.check_constraints
         WHERE name = N'CK_AccountingEvents_SourceCardinality'
           AND is_disabled = 1) AS DisabledCheckCount,
        (SELECT COUNT(*)
         FROM sys.foreign_keys
         WHERE name IN (N'FK_CustomerAdvanceApplications_AccountingEvents_RevenueEventId', N'FK_AccountingEvents_CustomerAdvanceApplications_ApplicationId')
           AND is_not_trusted = 1) AS UntrustedFKCount,
        (SELECT COUNT(*)
         FROM sys.check_constraints
         WHERE name = N'CK_AccountingEvents_SourceCardinality'
           AND is_not_trusted = 1) AS UntrustedCheckCount;

    DELETE FROM dbo.OrderItemFabrics;
    DELETE FROM dbo.OrderItems;
    DELETE FROM dbo.Orders;
    DELETE FROM dbo.Pieces;
    DELETE FROM dbo.PurchaseOrderItems;
    DELETE FROM dbo.PurchaseOrders;
    DELETE FROM dbo.Payments;
    DELETE FROM dbo.TrackingEvents;
    DELETE FROM dbo.AccountingEvents;
    DELETE FROM dbo.CustomerAdvanceApplications;
    DELETE FROM dbo.InventoryReceiptPostings;
    DELETE FROM dbo.GoodsReceiptItems;
    DELETE FROM dbo.SupplierInvoiceLines;
    DELETE FROM dbo.SupplierInvoices;
    DELETE FROM dbo.Suppliers;
    DELETE FROM dbo.GoodsReceipts;
    DELETE FROM dbo.FabricRolls;
    DELETE FROM dbo.UserSessions;

    CREATE TABLE #AfterCounts (
        TableName sysname NOT NULL PRIMARY KEY,
        RowsCount bigint NOT NULL
    );

    INSERT INTO #AfterCounts (TableName, RowsCount)
    SELECT t.TableName,
           (SELECT SUM(p.rows)
            FROM sys.partitions p
            WHERE p.object_id = OBJECT_ID(N'dbo.' + t.TableName)
              AND p.index_id IN (0,1))
    FROM #TargetTableList t;

    SELECT 'AFTER_TRANSACTION_COUNTS' AS Phase, TableName, RowsCount
    FROM #AfterCounts
    ORDER BY TableName;

    IF EXISTS (
        SELECT 1
        FROM #AfterCounts a
        JOIN #BeforeCounts b ON b.TableName = a.TableName
        WHERE a.RowsCount <> 0
    ) BEGIN
        THROW 50004, N'DIAGNOSTIC_CLEANUP_REACHED_ZERO_FAILED: not all approved residual rows were removed.', 1;
    END;

    SET @DeleteZero = 1;

    ALTER TABLE dbo.CustomerAdvanceApplications WITH CHECK CHECK CONSTRAINT FK_CustomerAdvanceApplications_AccountingEvents_RevenueEventId;
    ALTER TABLE dbo.AccountingEvents WITH CHECK CHECK CONSTRAINT FK_AccountingEvents_CustomerAdvanceApplications_ApplicationId;
    ALTER TABLE dbo.AccountingEvents WITH CHECK CHECK CONSTRAINT CK_AccountingEvents_SourceCardinality;

    SET @DisabledFKCount = (
        SELECT COUNT(*)
        FROM sys.foreign_keys
        WHERE name IN (N'FK_CustomerAdvanceApplications_AccountingEvents_RevenueEventId', N'FK_AccountingEvents_CustomerAdvanceApplications_ApplicationId')
          AND is_disabled = 1
    );

    SET @DisabledCheckCount = (
        SELECT COUNT(*)
        FROM sys.check_constraints
        WHERE name = N'CK_AccountingEvents_SourceCardinality'
          AND is_disabled = 1
    );

    SET @UntrustedFKCount = (
        SELECT COUNT(*)
        FROM sys.foreign_keys
        WHERE name IN (N'FK_CustomerAdvanceApplications_AccountingEvents_RevenueEventId', N'FK_AccountingEvents_CustomerAdvanceApplications_ApplicationId')
          AND is_not_trusted = 1
    );

    SET @UntrustedCheckCount = (
        SELECT COUNT(*)
        FROM sys.check_constraints
        WHERE name = N'CK_AccountingEvents_SourceCardinality'
          AND is_not_trusted = 1
    );

    IF @DisabledFKCount <> 0 OR @DisabledCheckCount <> 0 OR @UntrustedFKCount <> 0 OR @UntrustedCheckCount <> 0
    BEGIN
        THROW 50005, N'CONSTRAINT_REENABLE_VERIFICATION_FAILED: the exact named constraints were not restored to enabled trusted state.', 1;
    END;

    SET @ConstraintsReenabledAndTrusted = 1;

    ROLLBACK TRANSACTION;
    SET @RollbackVerified = 1;

    CREATE TABLE #PostRollbackCounts (
        TableName sysname NOT NULL PRIMARY KEY,
        RowsCount bigint NOT NULL
    );

    INSERT INTO #PostRollbackCounts (TableName, RowsCount)
    SELECT t.TableName,
           (SELECT SUM(p.rows)
            FROM sys.partitions p
            WHERE p.object_id = OBJECT_ID(N'dbo.' + t.TableName)
              AND p.index_id IN (0,1))
    FROM #TargetTableList t;

    IF EXISTS (
        SELECT 1
        FROM #PostRollbackCounts p
        JOIN #BeforeCounts b ON b.TableName = p.TableName
        WHERE p.RowsCount <> b.RowsCount
    ) BEGIN
        THROW 50006, N'ROLLBACK_VERIFICATION_FAILED: original counts did not return after rollback.', 1;
    END;

    SELECT
        @DeleteZero AS DiagnosticCleanupReachedZero,
        @ExactConstraintList AS ExactConstraintsTemporarilyDisabled,
        CAST(0 AS bit) AS GlobalConstraintDisableUsed,
        CAST(0 AS bit) AS GlobalTriggerDisableUsed,
        @ConstraintsReenabledAndTrusted AS ConstraintsReenabledAndTrustedBeforeRollback,
        @RollbackVerified AS RollbackVerified,
        CAST(0 AS bit) AS OutsideScopeDelete,
        CAST(0 AS bit) AS ProtectedDataModified,
        CAST(0 AS bit) AS SchemaPermanentlyModified,
        @DisabledFKCount AS DisabledFKCount,
        @UntrustedFKCount AS UntrustedFKCount,
        @DisabledCheckCount AS DisabledCheckCount,
        @UntrustedCheckCount AS UntrustedCheckCount,
        CAST(1 AS bit) AS OriginalRowsRestored;

    SELECT 'POST_ROLLBACK_COUNTS' AS Phase, TableName, RowsCount
    FROM #PostRollbackCounts
    ORDER BY TableName;

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    DECLARE @ErrorNumber int = ERROR_NUMBER();
    DECLARE @ErrorMessage nvarchar(4000) = ERROR_MESSAGE();

    SELECT
        @ErrorNumber AS ErrorNumber,
        @ErrorMessage AS ErrorMessage,
        @DeleteZero AS DiagnosticCleanupReachedZero,
        @ExactConstraintList AS ExactConstraintsTemporarilyDisabled,
        CAST(0 AS bit) AS GlobalConstraintDisableUsed,
        CAST(0 AS bit) AS GlobalTriggerDisableUsed,
        @ConstraintsReenabledAndTrusted AS ConstraintsReenabledAndTrustedBeforeRollback,
        @RollbackVerified AS RollbackVerified,
        CAST(0 AS bit) AS OutsideScopeDelete,
        CAST(0 AS bit) AS ProtectedDataModified,
        CAST(0 AS bit) AS SchemaPermanentlyModified,
        @DisabledFKCount AS DisabledFKCount,
        @UntrustedFKCount AS UntrustedFKCount,
        @DisabledCheckCount AS DisabledCheckCount,
        @UntrustedCheckCount AS UntrustedCheckCount;

    THROW;
END CATCH;
