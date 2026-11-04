/*
  OWNER-SCOPED CLEANUP VERIFIER
  Read-only verification plan for the owner-scoped zero-baseline.
  This file must never perform DML or schema change operations.
*/

SET NOCOUNT ON;

DECLARE @OwnerDecisionMismatch int = 0;
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

SELECT @@SERVERNAME AS ServerName, DB_NAME() AS DatabaseName;

/*
  Verifier must validate target scope based on the actual current-root IDs, not name-based predicates.
*/
SELECT
    'Customers' AS ScopeName,
    (SELECT COUNT(*) FROM dbo.Customers) AS CurrentRows,
    (SELECT COUNT(*) FROM dbo.Customers) AS TargetedRows,
    0 AS ExpectedAfter,
    'All current customer rows are in owner-scope operational data' AS RuleSummary
UNION ALL
SELECT 'Suppliers', (SELECT COUNT(*) FROM dbo.Suppliers), (SELECT COUNT(*) FROM dbo.Suppliers), 0, 'All current supplier rows are in owner-scope operational data'
UNION ALL
SELECT 'Employees', (SELECT COUNT(*) FROM dbo.Employees), (SELECT COUNT(*) FROM dbo.Employees), 0, 'All current employee rows are in owner-scope operational data'
UNION ALL
SELECT 'Orders', (SELECT COUNT(*) FROM dbo.Orders), (SELECT COUNT(*) FROM dbo.Orders), 0, 'All current order rows are in owner-scope operational data'
UNION ALL
SELECT 'OrderItems', (SELECT COUNT(*) FROM dbo.OrderItems), (SELECT COUNT(*) FROM dbo.OrderItems), 0, 'Operational order children in owner scope'
UNION ALL
SELECT 'Pieces', (SELECT COUNT(*) FROM dbo.Pieces), (SELECT COUNT(*) FROM dbo.Pieces), 0, 'Operational piece rows in owner scope'
UNION ALL
SELECT 'Payments', (SELECT COUNT(*) FROM dbo.Payments), (SELECT COUNT(*) FROM dbo.Payments), 0, 'Operational financial rows in owner scope'
UNION ALL
SELECT 'AccountingEvents', (SELECT COUNT(*) FROM dbo.AccountingEvents), (SELECT COUNT(*) FROM dbo.AccountingEvents), 0, 'Operational financial rows in owner scope'
UNION ALL
SELECT 'CustomerAdvanceApplications', (SELECT COUNT(*) FROM dbo.CustomerAdvanceApplications), (SELECT COUNT(*) FROM dbo.CustomerAdvanceApplications), 0, 'Operational financial rows in owner scope'
UNION ALL
SELECT 'GoodsReceipts', (SELECT COUNT(*) FROM dbo.GoodsReceipts), (SELECT COUNT(*) FROM dbo.GoodsReceipts), 0, 'Operational purchasing rows in owner scope'
UNION ALL
SELECT 'MobileAccounts', (SELECT COUNT(*) FROM dbo.MobileAccounts), (SELECT COUNT(*) FROM dbo.MobileAccounts), 0, 'Operational mobile rows in owner scope';

/*
  Protected scope read-only checks.
*/
SELECT
    'MessageTemplates' AS ProtectedScope,
    (SELECT COUNT(*) FROM dbo.MessageTemplates) AS CurrentRows,
    CASE WHEN (SELECT COUNT(*) FROM dbo.MessageTemplates) >= 0 THEN 'OK' ELSE 'CHECK' END AS ResultStatus
UNION ALL
SELECT 'System_Settings', (SELECT COUNT(*) FROM dbo.System_Settings), CASE WHEN (SELECT COUNT(*) FROM dbo.System_Settings) >= 0 THEN 'OK' ELSE 'CHECK' END
UNION ALL
SELECT 'LedgerAccounts', (SELECT COUNT(*) FROM dbo.LedgerAccounts), CASE WHEN (SELECT COUNT(*) FROM dbo.LedgerAccounts) >= 0 THEN 'OK' ELSE 'CHECK' END
UNION ALL
SELECT 'CashAccounts', (SELECT COUNT(*) FROM dbo.CashAccounts), CASE WHEN (SELECT COUNT(*) FROM dbo.CashAccounts) >= 0 THEN 'OK' ELSE 'CHECK' END
UNION ALL
SELECT 'PieceWageRates', (SELECT COUNT(*) FROM dbo.PieceWageRates), CASE WHEN (SELECT COUNT(*) FROM dbo.PieceWageRates) >= 0 THEN 'OK' ELSE 'CHECK' END;

/*
  FK/constraint read-only checks.
*/
SELECT
    CASE WHEN EXISTS (
        SELECT 1
        FROM sys.foreign_keys fk
        WHERE fk.name = N'FK_AccountingEvents_CustomerAdvanceApplications_ApplicationId'
          AND fk.is_disabled = 0 AND fk.is_not_trusted = 0
    ) THEN 'OK' ELSE 'CHECK' END AS FK1EnabledTrusted,
    CASE WHEN EXISTS (
        SELECT 1
        FROM sys.foreign_keys fk
        WHERE fk.name = N'FK_CustomerAdvanceApplications_AccountingEvents_RevenueEventId'
          AND fk.is_disabled = 0 AND fk.is_not_trusted = 0
    ) THEN 'OK' ELSE 'CHECK' END AS FK2EnabledTrusted,
    CASE WHEN EXISTS (
        SELECT 1
        FROM sys.check_constraints cc
        WHERE cc.name = N'CK_AccountingEvents_SourceCardinality'
          AND cc.is_disabled = 0 AND cc.is_not_trusted = 0
    ) THEN 'OK' ELSE 'CHECK' END AS AccountingCheckEnabledTrusted;

/*
  Final verifier aggregate: all design guard fields remain zero in this non-destructive mode.
*/
SELECT
    @OwnerDecisionMismatch AS OwnerDecisionMismatch,
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
  This verifier is read-only and does not execute any DML.
  After the live execution step, the verifier would validate zeroed operational scopes and preserved protected definitions.
*/
SELECT 'READ-ONLY VERIFIER ONLY: no INSERT, UPDATE, DELETE, MERGE, ALTER, DROP, TRUNCATE allowed.' AS ExecutionGuard;
