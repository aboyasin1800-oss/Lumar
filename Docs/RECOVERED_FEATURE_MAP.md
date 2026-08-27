# Rebuild Feature Map

This is a recovery planning map based on known module names and current table names. It does not represent recovered source code, completed functionality, or confirmed database relationships.

| Planned module | Candidate tables for future review |
| --- | --- |
| Customers | Customers, CustomerMeasurements, CustomerLedgerEntries, CustomerMessages, CustomerNotifications |
| Orders | Orders, OrderItems, OrderItemFabrics, Invoice_Header, Invoice_Details, Payments, Payments_Log |
| Production and tracking | ProductionOrders, ProductionBatches, Production_Tracking, TrackingEvents, Pieces, ProductMaterials, BillOfMaterials, ProductionMaterialConsumptions |
| Scanners | Scanners, Live_Scan, TrackingEvents |
| Piece wages | Piece_Rates, PieceWageRates, PieceWageRecords, Pieces, Piece_Measurements |
| Employees and advances | Employees, Departments, Employee_Draws, EmployeeDrawSettlements, EmployeeAttendances, EmployeeDocuments, Employee_Workflow, LeaveRequests |
| Payroll | PayrollPeriods, PayrollRecords, PayrollItems, Employees |
| Finance | FinancialTransactions, CashAccounts, LedgerAccounts, JournalEntries, JournalEntryLines, CustomerLedgerEntries, SupplierLedgerEntries |
| Inventory | InventoryItems, InventoryTransactions, InventoryValuationSnapshots, Fabrics, Fabrics_Inventory, FinishedProductReceipts, ReadyMadeInventoryProducts, ImportedReadyMadeProducts |
| Suppliers | Suppliers, PurchaseOrders, PurchaseOrderItems, GoodsReceipts, GoodsReceiptItems, SupplierInvoices, SupplierPayments, SupplierPaymentAllocations, SupplierTransactions |
| Loyalty | LoyaltyAccounts, LoyaltyProgramSettings, LoyaltyPiecePointSettings, LoyaltyRules, LoyaltyTransactions, LoyaltyRedemptions, LoyaltyRewards, VipLevels |
| Referrals | ReferralAccounts, ReferralCodes, ReferralRewards, ReferralTransactions, ReferralAnalytics |
| Messages | MessageTemplates, Messages_Templates, Sent_Messages, Sent_Log, CustomerMessages, CustomerNotifications |
| Order delivery | Orders, TrackingEvents, Production_Tracking, CustomerNotifications |
| Factory monitoring | PerformanceMetricRecords, SystemHealthRecords, ServiceAvailabilityRecords, AuditLogs, UserActivityLogs |
| Mobile identity | MobileAccounts, MobileSessions, MobileRecoveryChallenges, Users, UserClaimMappings, UserRoleAssignments, SecurityRoles, SecurityPermissions, RolePermissionMappings |