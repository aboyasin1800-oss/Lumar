SET NOCOUNT ON;
SELECT 'Customers' AS table_name, COUNT(*) AS row_count FROM dbo.Customers
UNION ALL SELECT 'Orders', COUNT(*) FROM dbo.Orders
UNION ALL SELECT 'OrderItems', COUNT(*) FROM dbo.OrderItems
UNION ALL SELECT 'Pieces', COUNT(*) FROM dbo.Pieces
UNION ALL SELECT 'Suppliers', COUNT(*) FROM dbo.Suppliers
UNION ALL SELECT 'GoodsReceipts', COUNT(*) FROM dbo.GoodsReceipts
UNION ALL SELECT 'GoodsReceiptItems', COUNT(*) FROM dbo.GoodsReceiptItems
UNION ALL SELECT 'InventoryTransactions', COUNT(*) FROM dbo.InventoryTransactions
UNION ALL SELECT 'AccountingEvents', COUNT(*) FROM dbo.AccountingEvents
UNION ALL SELECT 'Payments', COUNT(*) FROM dbo.Payments
UNION ALL SELECT 'CustomerMessages', COUNT(*) FROM dbo.CustomerMessages
UNION ALL SELECT 'MobileAccounts', COUNT(*) FROM dbo.MobileAccounts
UNION ALL SELECT 'LoyaltyAccounts', COUNT(*) FROM dbo.LoyaltyAccounts
UNION ALL SELECT 'ReferralCodes', COUNT(*) FROM dbo.ReferralCodes
UNION ALL SELECT 'Production_Tracking', COUNT(*) FROM dbo.Production_Tracking
UNION ALL SELECT 'Users', COUNT(*) FROM dbo.Users
UNION ALL SELECT 'System_Settings', COUNT(*) FROM dbo.System_Settings
UNION ALL SELECT 'CashAccounts', COUNT(*) FROM dbo.CashAccounts
UNION ALL SELECT 'LedgerAccounts', COUNT(*) FROM dbo.LedgerAccounts
UNION ALL SELECT '__EFMigrationsHistory', COUNT(*) FROM dbo.__EFMigrationsHistory
UNION ALL SELECT 'FabricConsumptionSources', COUNT(*) FROM dbo.FabricConsumptionSources
UNION ALL SELECT 'FabricConsumptionPartialReversals', COUNT(*) FROM dbo.FabricConsumptionPartialReversals
UNION ALL SELECT 'GoodsReceiptItemStorageAllocations', COUNT(*) FROM dbo.GoodsReceiptItemStorageAllocations
UNION ALL SELECT 'ScannerStageProductPermissions', COUNT(*) FROM dbo.ScannerStageProductPermissions;
