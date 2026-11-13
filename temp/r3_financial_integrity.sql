SELECT N'AccountingEvents_total_posting' AS Metric, CAST(SUM(PostingAmount) AS decimal(18,2)) AS Value FROM dbo.AccountingEvents;
SELECT N'FinancialTransactions_total_amount' AS Metric, CAST(SUM(Amount) AS decimal(18,2)) AS Value FROM dbo.FinancialTransactions;
SELECT N'JournalEntryLines_total_debit' AS Metric, CAST(SUM(DebitAmount) AS decimal(18,2)) AS Value FROM dbo.JournalEntryLines;
SELECT N'JournalEntryLines_total_credit' AS Metric, CAST(SUM(CreditAmount) AS decimal(18,2)) AS Value FROM dbo.JournalEntryLines;
SELECT N'JournalEntryLines_balance_diff' AS Metric, CAST(SUM(DebitAmount) - SUM(CreditAmount) AS decimal(18,2)) AS Value FROM dbo.JournalEntryLines;