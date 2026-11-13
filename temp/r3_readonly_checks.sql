SELECT N'SupplierInvoiceLines_missing_parents' AS Metric, COUNT(*) AS Value FROM dbo.SupplierInvoiceLines WHERE SupplierInvoiceId NOT IN (SELECT SupplierInvoiceId FROM dbo.SupplierInvoices);
SELECT N'GoodsReceiptItems_missing_parents' AS Metric, COUNT(*) AS Value FROM dbo.GoodsReceiptItems WHERE GoodsReceiptId NOT IN (SELECT GoodsReceiptId FROM dbo.GoodsReceipts);
SELECT N'InventoryTransactions_missing_inventory_items' AS Metric, COUNT(*) AS Value FROM dbo.InventoryTransactions WHERE InventoryItemId NOT IN (SELECT InventoryItemID FROM dbo.InventoryItems);
SELECT N'JournalEntryLines_missing_journal_entries' AS Metric, COUNT(*) AS Value FROM dbo.JournalEntryLines WHERE JournalEntryId NOT IN (SELECT JournalEntryId FROM dbo.JournalEntries);
SELECT N'FinancialTransactions_missing_accounting_events' AS Metric, COUNT(*) AS Value FROM dbo.FinancialTransactions WHERE AccountingEventId NOT IN (SELECT AccountingEventId FROM dbo.AccountingEvents);
SELECT N'JournalBalance_drift' AS Metric, COUNT(*) AS Value FROM (
    SELECT JournalEntryId, SUM(DebitAmount) AS JournalDebit, SUM(CreditAmount) AS JournalCredit
    FROM dbo.JournalEntryLines
    GROUP BY JournalEntryId
    HAVING ABS(SUM(DebitAmount) - SUM(CreditAmount)) > 0.01
) x;