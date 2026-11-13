SET NOCOUNT ON;
DECLARE @tables TABLE (Name sysname, ExpectedZero bit NOT NULL, Protected bit NOT NULL);
INSERT @tables(Name, ExpectedZero, Protected) VALUES
('Customers',1,0),('Suppliers',1,0),('Employees',1,0),('Orders',1,0),('OrderItems',1,0),('Pieces',1,0),('Payments',1,0),('AccountingEvents',1,0),('CustomerAdvanceApplications',1,0),('GoodsReceipts',1,0),('MobileAccounts',1,0),('MobileSessions',1,0),('MobilePushDevices',1,0),('MobileRecoveryChallenges',1,0),('JournalEntries',1,0),('JournalEntryLines',1,0),('MessageTemplates',0,1),('System_Settings',0,1),('LedgerAccounts',0,1),('CashAccounts',0,1),('PieceWageRates',0,1),('Users',0,1),('UserSessions',0,1);

SELECT t.Name,
       t.ExpectedZero,
       t.Protected,
       CASE WHEN OBJECT_ID(QUOTENAME(t.Name)) IS NULL THEN -1 ELSE (SELECT COUNT_BIG(*) FROM dbo.[" + ??? + "] ) END AS RowCount
FROM @tables t;
