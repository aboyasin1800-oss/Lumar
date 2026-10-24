SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() NOT IN (N'LUMAR_ERP_TEST', N'LUMAR_ERP_ES_VALIDATION')
    THROW 52990, N'This customer settlement discount migration is restricted to approved ES validation databases.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.LedgerAccounts', N'U') IS NULL
       OR OBJECT_ID(N'dbo.AccountRoleMappings', N'U') IS NULL
       OR OBJECT_ID(N'dbo.AccountingEventDefinitions', N'U') IS NULL
        THROW 52991, N'The official accounting tables are required before the settlement discount contract can be enforced.', 1;

    DECLARE @receivablesAccountId int = (SELECT TOP(1) LedgerAccountId FROM dbo.LedgerAccounts WHERE AccountCode = N'1200' AND IsActive = 1 ORDER BY LedgerAccountId);
    IF @receivablesAccountId IS NULL
        THROW 52992, N'The approved 1200 Accounts Receivable ledger must exist before this contract can be enabled.', 1;

    DECLARE @settlementDiscountAccountId int = (SELECT TOP(1) LedgerAccountId FROM dbo.LedgerAccounts WHERE AccountCode = N'4210' ORDER BY LedgerAccountId);
    IF @settlementDiscountAccountId IS NULL
    BEGIN
        INSERT INTO dbo.LedgerAccounts (AccountCode, AccountName, AccountType, IsActive, CreatedAt)
        VALUES (N'4210', N'Sales and Settlement Discounts', N'Revenue', 1, SYSUTCDATETIME());
        SET @settlementDiscountAccountId = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        UPDATE dbo.LedgerAccounts
           SET AccountName = N'Sales and Settlement Discounts',
               AccountType = N'Revenue',
               IsActive = 1,
               UpdatedAt = SYSUTCDATETIME()
         WHERE AccountCode = N'4210'
           AND (AccountName <> N'Sales and Settlement Discounts' OR AccountType <> N'Revenue' OR IsActive <> 1);

        SET @settlementDiscountAccountId = (SELECT TOP(1) LedgerAccountId FROM dbo.LedgerAccounts WHERE AccountCode = N'4210' ORDER BY LedgerAccountId);
    END;

    IF EXISTS (
        SELECT 1
        FROM dbo.AccountRoleMappings
        WHERE AccountRole = N'SalesSettlementDiscount'
          AND (LedgerAccountId <> @settlementDiscountAccountId OR IsEnabled <> 1)
    )
    BEGIN
        UPDATE dbo.AccountRoleMappings
           SET LedgerAccountId = @settlementDiscountAccountId,
               IsEnabled = 1
         WHERE AccountRole = N'SalesSettlementDiscount';
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.AccountRoleMappings WHERE AccountRole = N'SalesSettlementDiscount')
    BEGIN
        INSERT INTO dbo.AccountRoleMappings (AccountRole, LedgerAccountId, IsEnabled)
        VALUES (N'SalesSettlementDiscount', @settlementDiscountAccountId, 1);
    END;

    IF EXISTS (
        SELECT 1
        FROM dbo.AccountRoleMappings
        WHERE AccountRole = N'AccountsReceivable'
          AND (LedgerAccountId <> @receivablesAccountId OR IsEnabled <> 1)
    )
    BEGIN
        UPDATE dbo.AccountRoleMappings
           SET LedgerAccountId = @receivablesAccountId,
               IsEnabled = 1
         WHERE AccountRole = N'AccountsReceivable';
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.AccountRoleMappings WHERE AccountRole = N'AccountsReceivable')
    BEGIN
        INSERT INTO dbo.AccountRoleMappings (AccountRole, LedgerAccountId, IsEnabled)
        VALUES (N'AccountsReceivable', @receivablesAccountId, 1);
    END;

    IF EXISTS (
        SELECT 1
        FROM dbo.AccountingEventDefinitions
        WHERE AccountingEventType = 34
          AND (
              EventName <> N'CustomerSettlementDiscount'
              OR SourceType <> N'OrderSettlement'
              OR DebitAccountRole <> N'SalesSettlementDiscount'
              OR CreditAccountRole <> N'AccountsReceivable'
              OR RequiresCashMovement <> 0
              OR ExpenseImpact <> 0
              OR IsEnabled <> 1
              OR IsBusinessRuntimeEnabled <> 0
          )
    )
    BEGIN
        UPDATE dbo.AccountingEventDefinitions
           SET EventName = N'CustomerSettlementDiscount',
               SourceType = N'OrderSettlement',
               DebitAccountRole = N'SalesSettlementDiscount',
               CreditAccountRole = N'AccountsReceivable',
               CashDirection = NULL,
               RequiresCashMovement = 0,
               LiabilityImpact = 0,
               ExpenseImpact = 0,
               InventoryImpact = 0,
               IsEnabled = 1,
               IsBusinessRuntimeEnabled = 0
         WHERE AccountingEventType = 34;
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.AccountingEventDefinitions WHERE AccountingEventType = 34)
    BEGIN
        INSERT INTO dbo.AccountingEventDefinitions
            (AccountingEventType, EventName, SourceType, DebitAccountRole, CreditAccountRole, CashDirection,
             RequiresCashMovement, LiabilityImpact, ExpenseImpact, InventoryImpact, IsEnabled, IsBusinessRuntimeEnabled)
        VALUES
            (34, N'CustomerSettlementDiscount', N'OrderSettlement', N'SalesSettlementDiscount', N'AccountsReceivable', NULL, 0, 0, 0, 0, 1, 0);
    END;

    IF EXISTS (SELECT 1 FROM dbo.AccountingEventDefinitions WHERE AccountingEventType = 34 AND IsEnabled = 1)
    BEGIN
        DECLARE @eventRowCount int = (SELECT COUNT(*) FROM dbo.AccountingEventDefinitions WHERE AccountingEventType = 34);
        IF @eventRowCount <> 1
            THROW 52994, N'The settlement discount event contract must exist exactly once.', 1;
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
