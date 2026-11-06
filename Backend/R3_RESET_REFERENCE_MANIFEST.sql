SET NOCOUNT ON;
WITH c AS (
  SELECT s.name AS schema_name, t.name AS table_name, SUM(p.rows) AS current_row_count
  FROM sys.tables t
  JOIN sys.schemas s ON s.schema_id=t.schema_id
  LEFT JOIN sys.partitions p ON p.object_id=t.object_id AND p.index_id IN (0,1)
  WHERE t.is_ms_shipped=0
  GROUP BY s.name, t.name
)
SELECT schema_name, table_name, current_row_count,
       CASE
         WHEN table_name IN (
           'Customers','CustomerAdvanceApplications','CustomerLedgerEntries','CustomerMeasurements','CustomerMessages','CustomerNotifications',
           'Orders','OrderItems','Pieces','Piece_Measurements','Piece_Rates','Production_Tracking','ProductionOrders','ProductionBatches','ProductionMaterialConsumptions',
           'Employees','EmployeeAdvances','EmployeeAttendances','EmployeeContracts','EmployeeDailyAccruals','EmployeeDailyEligibility','EmployeeDailyExpenses',
           'EmployeeDrawSettlements','EmployeeLedgerEntries','EmployeePayments','EmployeePieceRateAssignments','EmployeeStatusHistory','EmployeeSeasonalBonuses','EmployeeDocuments',
           'Suppliers','SupplierInvoices','SupplierInvoiceLines','SupplierFinancialInvoices','SupplierFinancialPayments','SupplierFinancialPaymentAllocations',
           'SupplierLedgerEntries','SupplierPayments','SupplierPaymentAllocations','SupplierPaymentAcknowledgements','SupplierTransactions',
           'PurchaseOrders','PurchaseOrderItems','GoodsReceipts','GoodsReceiptItems','GoodsReceiptReversals','GoodsReceiptReversalLines','GoodsReceiptDifferences','InventoryItems','InventoryTransactions',
           'InventoryReceiptLines','InventoryReceiptPostings','InventoryItemFoundation','InventoryValuationSnapshots','FabricRolls','Fabrics','Fabrics_Inventory',
           'OrderItemFabrics','ProductMaterials','Products','ReadyMadeInventoryProducts','ReadyMadeProductionOrders','ReadyMadeProductionOrderItems','ReadyMadeProductionOrderPieceInstances',
           'ReadyMadeSaleCostPostings','ImportedReadyMadeInventoryReceipts','ImportedReadyMadeProducts','ImportedReadyMadeSaleCostPostings',
           'Payments','PaymentDisputes','Payments_Log','AccountingEvents','JournalEntries','JournalEntryLines','FinancialTransactions','CashMovements',
           'LoyaltyAccounts','LoyaltyRedemptions','LoyaltyRewards','LoyaltyTransactions','ReferralAccounts','ReferralAnalytics','ReferralCodes','ReferralRewards','ReferralTransactions',
           'MobileAccounts','MobileAccountCustomerAccess','MobileSessions','MobilePushDevices','MobileRecoveryChallenges','Sent_Log','Sent_Messages','CustomerNotifications','SupplierNotifications','SupplierMessages',
           'TrackingEvents','UserActivityLogs','UserSessions','MeasurementCardPrintHistory','ToolIssuances','ToolIssuanceReturns','ToolIssuanceReversals','TailoringCostPostings','TailoringOrderEditOperations',
           'TailoringPieceAddOperations','TailoringPieceBenefitBalances','TailoringPieceCancellations','TailoringPieceSaleValues','Live_Scan','RecoveryRecords','RestoreRecords','StageAttributionCorrections',
           'PerformanceMetricRecords','ServiceAvailabilityRecords','SupplierAnnouncements'
         ) THEN 'PURE_OPERATIONAL'
         WHEN table_name IN (
           'System_Settings','CashAccounts','LedgerAccounts','AccountingEventDefinitions','AccountRoleMappings','InventoryClassCatalog','InventoryUnitCatalog',
           'PricingProductTypes','PricingMeasurementFields','PricingMeasurementProfiles','PricingSizeClasses','PricingConsumptionRules','PricingRules','PricingMarginPolicies',
           'PricingCostItems','PricingCostMatrices','PricingCostMatrixItems','PricingWastePolicies','PricingApprovalHistory','PricingAuditLogs','PricingSimulationRuns',
           'VipLevels','VipLevelEvaluationCriteria','LoyaltyProgramSettings','LoyaltyPiecePointSettings','LoyaltyRules','LoyaltyReadyMadeProductTypePointSettings','LoyaltyImportedReadyMadeProductPointSettings',
           'MessageTemplates','Messages_Templates','EmployeeContractTemplates','Departments','Warehouses','Scanners','SecurityRoles','SecurityPermissions','RolePermissionMappings',
           'UserRoleAssignments','UserClaimMappings','Users','BackupTrustRecords','InventoryFoundationProcedureBackups','ProductMaterials','Products','InventoryItemFoundation'
         ) THEN 'PRESERVE_BASELINE'
         WHEN table_name IN (
           'AuditLogs','ApiFailureLogs','BackupRecords','BillOfMaterials','CancelledPieceDisposition','CustomerMeasurements','CustomerMessages','CustomerNotifications','Employee_Workflow','Employee_Draws',
           'EmployeeDocuments','EmployeeSeasonalBonuses','EmployeeStatusHistory','FinancialTransactions','FinishedProductReceipts','Invoice_Details','Invoice_Header','LeaveRequests','MeasurementCardPrintHistory',
           'PaymentDisputes','PayrollItems','PayrollPeriods','PayrollRecords','PieceWageRates','PieceWageRecords','ProductionOrders','ProductionMaterialConsumptions','ReferralAnalytics','SecurityDriftRecords',
           'SecurityEventLogs','Sent_Log','Sent_Messages','SupplierFinancialPaymentAllocations','SupplierLedgerEntries','SystemHealthRecords','TailoringPieceBenefitBalances','TailoringPieceCancellations',
           'TrackingEvents','UserActivityLogs','UserSessions','VipLevelEvaluationCriteria'
         ) THEN 'MIXED_RULE'
         ELSE 'EXCEPTION_REVIEW'
       END AS current_state_role,
       CASE
         WHEN table_name IN (
           'Customers','CustomerAdvanceApplications','CustomerLedgerEntries','CustomerMeasurements','CustomerMessages','CustomerNotifications','Orders','OrderItems','Pieces','Piece_Measurements','Piece_Rates','Production_Tracking','ProductionOrders','ProductionBatches','ProductionMaterialConsumptions',
           'Employees','EmployeeAdvances','EmployeeAttendances','EmployeeContracts','EmployeeDailyAccruals','EmployeeDailyEligibility','EmployeeDailyExpenses','EmployeeDrawSettlements','EmployeeLedgerEntries','EmployeePayments','EmployeePieceRateAssignments','EmployeeStatusHistory','EmployeeSeasonalBonuses','EmployeeDocuments',
           'Suppliers','SupplierInvoices','SupplierInvoiceLines','SupplierFinancialInvoices','SupplierFinancialPayments','SupplierFinancialPaymentAllocations','SupplierLedgerEntries','SupplierPayments','SupplierPaymentAllocations','SupplierPaymentAcknowledgements','SupplierTransactions',
           'PurchaseOrders','PurchaseOrderItems','GoodsReceipts','GoodsReceiptItems','GoodsReceiptReversals','GoodsReceiptReversalLines','GoodsReceiptDifferences','InventoryItems','InventoryTransactions','InventoryReceiptLines','InventoryReceiptPostings','FabricRolls','Fabrics','Fabrics_Inventory','OrderItemFabrics',
           'Products','ReadyMadeInventoryProducts','ReadyMadeProductionOrders','ReadyMadeProductionOrderItems','ReadyMadeProductionOrderPieceInstances','ReadyMadeSaleCostPostings','ImportedReadyMadeInventoryReceipts','ImportedReadyMadeProducts','ImportedReadyMadeSaleCostPostings',
           'Payments','PaymentDisputes','Payments_Log','AccountingEvents','JournalEntries','JournalEntryLines','FinancialTransactions','CashMovements',
           'LoyaltyAccounts','LoyaltyRedemptions','LoyaltyRewards','LoyaltyTransactions','ReferralAccounts','ReferralAnalytics','ReferralCodes','ReferralRewards','ReferralTransactions',
           'MobileAccounts','MobileAccountCustomerAccess','MobileSessions','MobilePushDevices','MobileRecoveryChallenges','Sent_Log','Sent_Messages','SupplierNotifications','SupplierMessages','TrackingEvents','UserActivityLogs','UserSessions'
         ) THEN 'RESET_OPERATIONAL_SCOPE'
         ELSE 'PRESERVE_BASELINE_SCOPE'
       END AS reset_scope,
       CASE
         WHEN table_name IN (
           'System_Settings','CashAccounts','LedgerAccounts','AccountingEventDefinitions','AccountRoleMappings','InventoryClassCatalog','InventoryUnitCatalog',
           'PricingProductTypes','PricingMeasurementFields','PricingMeasurementProfiles','PricingSizeClasses','PricingConsumptionRules','PricingRules','PricingMarginPolicies',
           'PricingCostItems','PricingCostMatrices','PricingCostMatrixItems','PricingWastePolicies','PricingApprovalHistory','PricingAuditLogs','PricingSimulationRuns',
           'VipLevels','VipLevelEvaluationCriteria','LoyaltyProgramSettings','LoyaltyPiecePointSettings','LoyaltyRules','LoyaltyReadyMadeProductTypePointSettings','LoyaltyImportedReadyMadeProductPointSettings',
           'MessageTemplates','Messages_Templates','EmployeeContractTemplates','Departments','Warehouses','Scanners','Users','SecurityRoles','SecurityPermissions','RolePermissionMappings','UserRoleAssignments','UserClaimMappings'
         ) THEN 'PROTECT_CURRENT_BASELINE'
         ELSE 'RETAIN_CURRENT_IDENTITY'
       END AS preserve_scope,
       CASE
         WHEN table_name IN (
           'System_Settings','CashAccounts','LedgerAccounts','AccountingEventDefinitions','AccountRoleMappings','InventoryClassCatalog','InventoryUnitCatalog',
           'PricingProductTypes','MessageTemplates','Messages_Templates','SecurityRoles','SecurityPermissions','RolePermissionMappings','UserRoleAssignments','UserClaimMappings','Users'
         ) THEN 'PRIMARY_KEY_AND_REFERENCE_VALUES'
         WHEN table_name IN ('Customers','Orders','OrderItems','Pieces','Employees','Suppliers','GoodsReceipts','PurchaseOrders','Payments','AccountingEvents','InventoryItems','FabricRolls','Products') THEN 'PARENT_CHILD_FK_AND_KEY_RANGE'
         ELSE 'CURRENT_ROWSET_AND_FK_INTEGRITY'
       END AS baseline_verification_method,
       CASE
         WHEN table_name IN ('Customers','Orders','OrderItems','Pieces','Employees','Suppliers','GoodsReceipts','PurchaseOrders','Payments','AccountingEvents','InventoryItems','FabricRolls','Products','MobileAccounts','LoyaltyAccounts','ReferralAccounts') THEN 'Operational lifecycle table representing test-created business activity; baseline currently clean and should be restored to same reference state after reset.'
         WHEN table_name IN ('System_Settings','CashAccounts','LedgerAccounts','AccountingEventDefinitions','SecurityRoles','SecurityPermissions','RolePermissionMappings','Users','MessageTemplates','Messages_Templates') THEN 'Protected baseline definition, security foundation, or config template; must remain aligned with current clean reference.'
         WHEN table_name IN ('AuditLogs','ApiFailureLogs','BackupRecords','TrackingEvents','UserActivityLogs','UserSessions','SecurityEventLogs') THEN 'Mixed or system-scope table; keep current baseline unless business rule explicitly marks it as operational runtime data.'
         ELSE 'Current clean state must be preserved as reference baseline; no destructive action is implied by this file.'
       END AS notes
FROM c
ORDER BY schema_name, table_name;
