SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.AccountingEvents', N'U') IS NULL
        THROW 51030, N'AccountingEvents foundation must exist before future-only enforcement.', 1;

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_FinancialTransactions_AccountingEventFoundation')
    BEGIN
        ALTER TABLE dbo.FinancialTransactions WITH NOCHECK
            ADD CONSTRAINT CK_FinancialTransactions_AccountingEventFoundation CHECK
            (
                TransactionType NOT IN
                (
                    N'CustomerAdvance',
                    N'CustomerPayment',
                    N'RevenueRecognized',
                    N'WipToFinishedGoods'
                )
                OR AccountingEventId IS NOT NULL
            );
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_JournalEntries_AccountingEventFoundation')
    BEGIN
        ALTER TABLE dbo.JournalEntries WITH NOCHECK
            ADD CONSTRAINT CK_JournalEntries_AccountingEventFoundation CHECK
            (
                AccountingEventId IS NOT NULL
            );
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;