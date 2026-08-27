# LUMAR ERP Database Inventory

Inventory captured by a read-only query against `YASIN-YASIN\SQLEXPRESS` / `LUMAR_ERP`. It is an inventory for planning only; no schema, data, MDF, or LDF files were copied or changed.

## Tables

```text
__EFMigrationsHistory, ApiFailureLogs, AuditLogs, BackupRecords,
BackupTrustRecords, BillOfMaterials, CashAccounts, CustomerLedgerEntries,
CustomerMeasurements, CustomerMessages, CustomerNotifications, Customers,
Departments, Employee_Draws, Employee_Workflow, EmployeeAttendances,
EmployeeDocuments, EmployeeDrawSettlements, Employees, Fabrics,
Fabrics_Inventory, FinancialTransactions, FinishedProductReceipts,
GoodsReceiptItems, GoodsReceipts, ImportedReadyMadeProducts, InventoryItems,
InventoryTransactions, InventoryValuationSnapshots, Invoice_Details,
Invoice_Header, JournalEntries, JournalEntryLines, LeaveRequests,
LedgerAccounts, Live_Scan, LoyaltyAccounts, LoyaltyPiecePointSettings,
LoyaltyProgramSettings, LoyaltyRedemptions, LoyaltyRewards, LoyaltyRules,
LoyaltyTransactions, Messages_Templates, MessageTemplates, MobileAccounts,
MobileRecoveryChallenges, MobileSessions, OrderItemFabrics, OrderItems,
Orders, Payments, Payments_Log, PayrollItems, PayrollPeriods, PayrollRecords,
PerformanceMetricRecords, Piece_Measurements, Piece_Rates, Pieces,
PieceWageRates, PieceWageRecords, PricingApprovalHistory, PricingAuditLogs,
PricingConsumptionRules, PricingCostItems, PricingCostMatrices,
PricingCostMatrixItems, PricingMarginPolicies, PricingMeasurementFields,
PricingMeasurementProfiles, PricingProductTypes, PricingRules,
PricingSimulationRuns, PricingSizeClasses, PricingWastePolicies,
Production_Tracking, ProductionBatches, ProductionMaterialConsumptions,
ProductionOrders, ProductMaterials, Products, PurchaseOrderItems,
PurchaseOrders, ReadyMadeInventoryProducts, ReadyMadeProductionOrderItems,
ReadyMadeProductionOrderPieceInstances, ReadyMadeProductionOrders,
RecoveryRecords, ReferralAccounts, ReferralAnalytics, ReferralCodes,
ReferralRewards, ReferralTransactions, RestoreRecords, RolePermissionMappings,
Scanners, SecurityDriftRecords, SecurityEventLogs, SecurityPermissions,
SecurityRoles, Sent_Log, Sent_Messages, ServiceAvailabilityRecords,
SupplierInvoices, SupplierLedgerEntries, SupplierPaymentAllocations,
SupplierPayments, Suppliers, SupplierTransactions, System_Settings,
SystemHealthRecords, TrackingEvents, UserActivityLogs, UserClaimMappings,
UserRoleAssignments, Users, VipLevels
```

Table-to-module groupings in `RECOVERED_FEATURE_MAP.md` are planning associations inferred from table names, not reconstructed application behavior or verified foreign-key contracts.