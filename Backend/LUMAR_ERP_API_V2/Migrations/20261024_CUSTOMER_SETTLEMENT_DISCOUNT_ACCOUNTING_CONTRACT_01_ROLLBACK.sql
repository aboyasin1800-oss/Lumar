SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() NOT IN (N'LUMAR_ERP_TEST', N'LUMAR_ERP_ES_VALIDATION')
    THROW 52995, N'This rollback is restricted to approved ES validation databases.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF EXISTS (SELECT 1 FROM dbo.AccountingEvents WHERE AccountingEventType = 34)
        THROW 52996, N'Cannot roll back the settlement discount contract because accounting events already exist for it.', 1;

    IF EXISTS (SELECT 1 FROM dbo.FinancialTransactions WHERE TransactionType = N'CustomerSettlementDiscount')
        THROW 52997, N'Cannot roll back the settlement discount contract because financial transactions already exist for it.', 1;

    IF EXISTS (SELECT 1 FROM dbo.JournalEntryLines jel JOIN dbo.LedgerAccounts la ON la.LedgerAccountId = jel.LedgerAccountId WHERE la.AccountCode = N'4210')
        THROW 52998, N'Cannot roll back the settlement discount contract because 4210 has live journal lines.', 1;

    DELETE FROM dbo.AccountingEventDefinitions WHERE AccountingEventType = 34;
    DELETE FROM dbo.AccountRoleMappings WHERE AccountRole = N'SalesSettlementDiscount';
    DELETE FROM dbo.AccountRoleMappings WHERE AccountRole = N'AccountsReceivable';

    DELETE FROM dbo.LedgerAccounts WHERE AccountCode = N'4210';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
