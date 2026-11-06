SET NOCOUNT ON;

SELECT 'TableCounts' AS CheckName;
SELECT 'FinancialTransactions' AS Name, COUNT(*) AS Value FROM dbo.FinancialTransactions UNION ALL
SELECT 'JournalEntries', COUNT(*) FROM dbo.JournalEntries UNION ALL
SELECT 'JournalEntryLines', COUNT(*) FROM dbo.JournalEntryLines UNION ALL
SELECT 'CashMovements', COUNT(*) FROM dbo.CashMovements UNION ALL
SELECT 'CustomerLedgerEntries', COUNT(*) FROM dbo.CustomerLedgerEntries UNION ALL
SELECT 'SupplierLedgerEntries', COUNT(*) FROM dbo.SupplierLedgerEntries UNION ALL
SELECT 'PayrollItems', COUNT(*) FROM dbo.PayrollItems UNION ALL
SELECT 'Orders', COUNT(*) FROM dbo.Orders UNION ALL
SELECT 'Purchases', COUNT(*) FROM dbo.Purchases;

SELECT 'UnbalancedJournals' AS CheckName, COUNT(*) AS Value
FROM (
  SELECT je.JournalEntryId
  FROM dbo.JournalEntries je
  LEFT JOIN dbo.JournalEntryLines jel ON jel.JournalEntryId = je.JournalEntryId
  GROUP BY je.JournalEntryId
  HAVING ABS(COALESCE(SUM(jel.DebitAmount),0) - COALESCE(SUM(jel.CreditAmount),0)) > 0.005
) x;

SELECT 'OrphanJournalLines' AS CheckName, COUNT(*) AS Value
FROM dbo.JournalEntryLines jel
LEFT JOIN dbo.JournalEntries je ON je.JournalEntryId = jel.JournalEntryId
WHERE je.JournalEntryId IS NULL;

SELECT 'DuplicateFinancialReferenceNumbers' AS CheckName, COUNT(*) AS Value
FROM (
  SELECT ReferenceNumber
  FROM dbo.FinancialTransactions
  GROUP BY ReferenceNumber
  HAVING COUNT(*) > 1
) x;

SELECT 'DuplicateJournalReferenceNumbers' AS CheckName, COUNT(*) AS Value
FROM (
  SELECT ReferenceNumber
  FROM dbo.JournalEntries
  GROUP BY ReferenceNumber
  HAVING COUNT(*) > 1
) x;

SELECT 'DuplicateCashReferenceNumbers' AS CheckName, COUNT(*) AS Value
FROM (
  SELECT ReferenceNumber
  FROM dbo.CashMovements
  GROUP BY ReferenceNumber
  HAVING COUNT(*) > 1
) x;

SELECT 'RevenueByType' AS CheckName;
SELECT TransactionType, COUNT(*) AS CountValue, CAST(SUM(Amount) AS decimal(18,2)) AS TotalAmount
FROM dbo.FinancialTransactions
GROUP BY TransactionType
ORDER BY TransactionType;

SELECT 'CashSignedNet' AS CheckName, CAST(COALESCE(SUM(CASE WHEN CashDirection = 1 THEN Amount WHEN CashDirection = 2 THEN -Amount ELSE 0 END),0) AS decimal(18,2)) AS Value
FROM dbo.CashMovements;

SELECT 'CashAccountLedgerNet' AS CheckName, CAST(COALESCE(SUM(jel.DebitAmount - jel.CreditAmount),0) AS decimal(18,2)) AS Value
FROM dbo.JournalEntryLines jel
INNER JOIN dbo.LedgerAccounts la ON la.LedgerAccountId = jel.LedgerAccountId
WHERE la.AccountCode = '1000';

SELECT 'CustomerLedgerBalanceTotal' AS CheckName, CAST(COALESCE(SUM(BalanceAfterTransaction),0) AS decimal(18,2)) AS Value
FROM dbo.CustomerLedgerEntries;

SELECT 'SupplierLedgerBalanceTotal' AS CheckName, CAST(COALESCE(SUM(BalanceAfterTransaction),0) AS decimal(18,2)) AS Value
FROM dbo.SupplierLedgerEntries;

SELECT 'MinMaxJournalEntryDates' AS CheckName;
SELECT MIN(EntryDate) AS MinEntryDate, MAX(EntryDate) AS MaxEntryDate, COUNT(*) AS JournalCount FROM dbo.JournalEntries;

SELECT 'UnpostedFinancialEvents' AS CheckName, COUNT(*) AS Value
FROM dbo.AccountingEvents ae
LEFT JOIN dbo.JournalEntries je ON je.AccountingEventId = ae.AccountingEventId
WHERE je.AccountingEventId IS NULL AND ae.IsPosted = 1;
