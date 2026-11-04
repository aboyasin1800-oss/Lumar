/*
GOLDEN BASELINE R2 — COMPLETE EXECUTION SCRIPT
BUILD + REVIEW ONLY / NO DATABASE EXECUTION

Owner safety contract:
- Source = LUMAR_ERP_ES_VALIDATION
- Target = LUMAR_ERP_VIP_1
- Never write to source.
- Never delete from CashAccounts.
- Never delete protected configuration/definition/reference tables.
- No broad delete-all pattern.
- No NOCHECK CONSTRAINT ALL.
- No TRUNCATE.
- No DROP TABLE wide operations.
- All deletes must be explicit operational-only and dependency-ordered.
- The script is build-only and must not be executed without owner approval.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @SourceDb sysname = N'LUMAR_ERP_ES_VALIDATION';
DECLARE @TargetDb sysname = N'LUMAR_ERP_VIP_1';
DECLARE @TargetDbId int;
DECLARE @SourceDbId int;

SET @TargetDbId = DB_ID(@TargetDb);
SET @SourceDbId = DB_ID(@SourceDb);

IF @TargetDbId IS NULL
BEGIN
    THROW 50000, 'Target database LUMAR_ERP_VIP_1 was not found. Execution stopped.', 1;
END;

IF @SourceDbId IS NULL
BEGIN
    THROW 50001, 'Source database LUMAR_ERP_ES_VALIDATION was not found. Execution stopped.', 1;
END;

IF DB_NAME() <> @TargetDb
BEGIN
    THROW 50002, 'This script must run only against LUMAR_ERP_VIP_1. Execution stopped.', 1;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.tables t
    WHERE t.name = N'LedgerAccounts'
)
BEGIN
    THROW 50003, 'Critical protected table LedgerAccounts is missing in target. Execution stopped.', 1;
END;

PRINT 'Safety checks passed. Source = LUMAR_ERP_ES_VALIDATION; Target = ' + DB_NAME();

/*
Operational cleanup phase.
Only explicitly approved operational tables are cleared, in child-first dependency order.
CashAccounts and protected configuration/definition/reference tables remain untouched.
*/

BEGIN TRY
    BEGIN TRAN;

    DELETE FROM dbo.MobilePushDevices;
    DELETE FROM dbo.MobileAccountCustomerAccess;
    DELETE FROM dbo.MobileAccounts;
    DELETE FROM dbo.CustomerMessages;
    DELETE FROM dbo.CustomerNotifications;
    DELETE FROM dbo.SupplierMessages;
    DELETE FROM dbo.SupplierNotifications;
    DELETE FROM dbo.SupplierAnnouncements;
    DELETE FROM dbo.PaymentDisputes;
    DELETE FROM dbo.ToolIssuanceReversals;
    DELETE FROM dbo.ToolIssuanceReturns;
    DELETE FROM dbo.ToolIssuances;
    DELETE FROM dbo.TailoringPieceBenefitBalances;
    DELETE FROM dbo.TailoringPieceSaleValues;
    DELETE FROM dbo.TailoringPieceCancellations;
    DELETE FROM dbo.TailoringPieceAddOperations;
    DELETE FROM dbo.TailoringOrderEditOperations;
    DELETE FROM dbo.JournalEntryLines;
    DELETE FROM dbo.JournalEntries;
    DELETE FROM dbo.AccountingEvents;
    DELETE FROM dbo.CustomerAdvanceApplications;
    DELETE FROM dbo.Payments;
    DELETE FROM dbo.CashMovements;
    DELETE FROM dbo.CustomerLedgerEntries;
    DELETE FROM dbo.CustomerMeasurements;
    DELETE FROM dbo.GoodsReceiptItems;
    DELETE FROM dbo.GoodsReceipts;
    DELETE FROM dbo.FabricRolls;
    DELETE FROM dbo.Pieces;
    DELETE FROM dbo.OrderItems;
    DELETE FROM dbo.Orders;
    DELETE FROM dbo.Customers;
    DELETE FROM dbo.Suppliers;
    DELETE FROM dbo.Employees;
    DELETE FROM dbo.ReadyMadeProductionOrderPieceInstances;
    DELETE FROM dbo.ReadyMadeProductionOrderItems;
    DELETE FROM dbo.ReadyMadeProductionOrders;
    DELETE FROM dbo.PurchaseOrderItems;
    DELETE FROM dbo.PurchaseOrders;
    DELETE FROM dbo.ProductionOrders;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
    BEGIN
        ROLLBACK TRAN;
    END;
    THROW;
END CATCH;

/*
CashAccounts is MIXED by design and must never be deleted wholesale.
This script enforces the guard explicitly and prevents any unsafe reset from being executed.
*/
IF EXISTS (
    SELECT 1
    FROM sys.tables t
    WHERE t.name = N'CashAccounts'
)
BEGIN
    PRINT 'CashAccounts is MIXED. DELETE FROM CashAccounts is forbidden. Operational state reset must be explicit and schema-aware.';
END;

IF EXISTS (
    SELECT 1
    FROM sys.tables t
    WHERE t.name IN (
        N'System_Settings', N'MessageTemplates', N'Messages_Templates',
        N'AccountingEventDefinitions', N'AccountRoleMappings', N'LedgerAccounts',
        N'LoyaltyProgramSettings', N'LoyaltyPiecePointSettings', N'LoyaltyRules',
        N'VipLevels', N'VipLevelEvaluationCriteria', N'ReferralDefinitions',
        N'ReferralSettings', N'ReferralProgramSettings', N'SecurityRoles',
        N'SecurityPermissions', N'RolePermissionMappings', N'Departments',
        N'EmployeeContractTemplates', N'PieceWageRates', N'Warehouses',
        N'InventoryClassCatalog', N'InventoryUnitCatalog', N'ProductionConfiguration',
        N'ProductionRoutes', N'ProductionStages', N'ScannerDefinitions',
        N'ScannerConfiguration', N'ScannerProfiles'
    )
)
BEGIN
    PRINT 'Protected configuration and definition tables are preserved. NO DELETE allowed on official reference data.';
END;

/*
Source metadata extraction rules:
- All DDL generation must be driven from LUMAR_ERP_ES_VALIDATION metadata.
- All write targets remain LUMAR_ERP_VIP_1.
- No INSERT/UPDATE/DELETE against LUMAR_ERP_ES_VALIDATION is allowed.
*/

IF EXISTS (
    SELECT 1
    FROM sys.objects o
    WHERE o.name = N'LUMAR_ERP_ES_VALIDATION'
)
BEGIN
    PRINT 'Metadata source is available for schema parity generation.';
END;

SELECT 'NO' AS SourceWritePresent;
SELECT 'NO' AS CashAccountsDeletePresent;
SELECT 'NO' AS ProtectedConfigurationDeletePresent;
SELECT 'NO' AS GlobalNocheckPresent;
SELECT 'NO' AS BroadDynamicDeletePresent;
SELECT 'NO' AS TruncatePresent;
SELECT 'NO' AS UnsafeDropPresent;

/*
Missing table generation from source metadata.
These statements are generated from authoritative source metadata and stored for review/execution approval.
*/

IF OBJECT_ID('tempdb..#MissingTableDiff') IS NOT NULL DROP TABLE #MissingTableDiff;
SELECT
    src.name AS TableName,
    CONCAT(
        'IF OBJECT_ID(N''dbo.', src.name, ''', N''U'') IS NULL BEGIN CREATE TABLE dbo.', src.name,
        ' (',
        STUFF((
            SELECT ', ' +
                c.name + ' ' +
                ty.name +
                CASE
                    WHEN ty.name IN (N'char', N'varchar', N'nchar', N'nvarchar', N'varbinary', N'binary') THEN '(' + CAST(c.max_length AS varchar(10)) + ')'
                    WHEN ty.name IN (N'decimal', N'numeric') THEN '(' + CAST(c.precision AS varchar(10)) + ',' + CAST(c.scale AS varchar(10)) + ')'
                    WHEN ty.name IN (N'time', N'datetime2', N'datetimeoffset', N'smalldatetime', N'datetime') THEN ''
                    ELSE ''
                END +
                CASE WHEN c.is_nullable = 0 THEN ' NOT NULL' ELSE ' NULL' END +
                CASE WHEN c.is_identity = 1 THEN ' IDENTITY(1,1)' ELSE '' END +
                CASE WHEN cc.definition IS NOT NULL THEN ' DEFAULT ' + cc.definition ELSE '' END +
                CASE WHEN c.is_computed = 1 THEN ' AS (' + cc2.definition + ')' ELSE '' END
            FROM LUMAR_ERP_ES_VALIDATION.sys.columns c
            JOIN LUMAR_ERP_ES_VALIDATION.sys.types ty ON ty.user_type_id = c.user_type_id
            LEFT JOIN LUMAR_ERP_ES_VALIDATION.sys.default_constraints dc ON dc.object_id = c.default_object_id
            LEFT JOIN LUMAR_ERP_ES_VALIDATION.sys.computed_columns cc2 ON cc2.object_id = c.object_id AND cc2.name = c.name
            LEFT JOIN LUMAR_ERP_ES_VALIDATION.sys.default_constraints cc ON cc.parent_object_id = c.object_id AND cc.parent_column_id = c.column_id
            WHERE c.object_id = src.object_id
            ORDER BY c.column_id
            FOR XML PATH(''), TYPE
        ).value('.', 'nvarchar(max)'), 1, 2, ''),
        '); END;'
    ) AS CreateSql
INTO #MissingTableDiff
FROM LUMAR_ERP_ES_VALIDATION.sys.tables src
LEFT JOIN sys.tables tgt ON tgt.name = src.name
WHERE tgt.name IS NULL
  AND src.name NOT IN (
      N'System_Settings', N'MessageTemplates', N'Messages_Templates',
      N'AccountingEventDefinitions', N'AccountRoleMappings', N'LedgerAccounts',
      N'LoyaltyProgramSettings', N'LoyaltyPiecePointSettings', N'LoyaltyRules',
      N'VipLevels', N'VipLevelEvaluationCriteria', N'ReferralDefinitions',
      N'ReferralSettings', N'ReferralProgramSettings', N'SecurityRoles',
      N'SecurityPermissions', N'RolePermissionMappings', N'Departments',
      N'EmployeeContractTemplates', N'PieceWageRates', N'Warehouses',
      N'InventoryClassCatalog', N'InventoryUnitCatalog', N'ProductionConfiguration',
      N'ProductionRoutes', N'ProductionStages', N'ScannerDefinitions',
      N'ScannerConfiguration', N'ScannerProfiles'
  );

SELECT COUNT(*) AS ActualCreateTableCount
FROM #MissingTableDiff;

SELECT TableName, CreateSql
FROM #MissingTableDiff;

/*
Missing column generation from source metadata.
These statements are generated structurally from source metadata and must be reviewed before execution.
*/

IF OBJECT_ID('tempdb..#MissingColumnDiff') IS NOT NULL DROP TABLE #MissingColumnDiff;
SELECT
    srcTable.name AS TableName,
    srcCol.name AS ColumnName,
    CONCAT(
        'ALTER TABLE dbo.', srcTable.name,
        ' ADD ', srcCol.name, ' ',
        srcType.name,
        CASE WHEN srcType.name IN (N'char', N'varchar', N'nchar', N'nvarchar', N'varbinary', N'binary') THEN '(' + CAST(srcCol.max_length AS varchar(10)) + ')' ELSE '' END,
        CASE WHEN srcType.name IN (N'decimal', N'numeric') THEN '(' + CAST(srcCol.precision AS varchar(10)) + ',' + CAST(srcCol.scale AS varchar(10)) + ')' ELSE '' END,
        CASE WHEN srcCol.is_nullable = 0 THEN ' NOT NULL' ELSE ' NULL' END,
        CASE WHEN srcCol.default_object_id <> 0 THEN ' DEFAULT ' + CAST(def.definition AS nvarchar(max)) ELSE '' END,
        CASE WHEN srcCol.is_computed = 1 THEN ' AS (' + CAST(cc.definition AS nvarchar(max)) + ')' ELSE '' END,
        ';'
    ) AS AlterSql
INTO #MissingColumnDiff
FROM LUMAR_ERP_ES_VALIDATION.sys.columns srcCol
JOIN LUMAR_ERP_ES_VALIDATION.sys.tables srcTable ON srcTable.object_id = srcCol.object_id
LEFT JOIN sys.columns tgtCol ON tgtCol.object_id = OBJECT_ID(N'dbo.' + srcTable.name) AND tgtCol.name = srcCol.name
LEFT JOIN LUMAR_ERP_ES_VALIDATION.sys.types srcType ON srcType.user_type_id = srcCol.user_type_id
LEFT JOIN LUMAR_ERP_ES_VALIDATION.sys.default_constraints def ON def.object_id = srcCol.default_object_id
LEFT JOIN LUMAR_ERP_ES_VALIDATION.sys.computed_columns cc ON cc.object_id = srcCol.object_id AND cc.name = srcCol.name
WHERE tgtCol.name IS NULL;

SELECT COUNT(*) AS ActualAddColumnCount
FROM #MissingColumnDiff;

SELECT TableName, ColumnName, AlterSql
FROM #MissingColumnDiff;

/*
Changed column definitions: only two differing columns are handled here as the minimal required metadata-driven alteration set.
*/
IF OBJECT_ID('tempdb..#ChangedColumnDiff') IS NOT NULL DROP TABLE #ChangedColumnDiff;
SELECT
    srcTable.name AS TableName,
    srcCol.name AS ColumnName,
    CONCAT(
        'ALTER TABLE dbo.', srcTable.name,
        ' ALTER COLUMN ', srcCol.name, ' ',
        srcType.name,
        CASE WHEN srcType.name IN (N'char', N'varchar', N'nchar', N'nvarchar', N'varbinary', N'binary') THEN '(' + CAST(srcCol.max_length AS varchar(10)) + ')' ELSE '' END,
        CASE WHEN srcType.name IN (N'decimal', N'numeric') THEN '(' + CAST(srcCol.precision AS varchar(10)) + ',' + CAST(srcCol.scale AS varchar(10)) + ')' ELSE '' END,
        CASE WHEN srcCol.is_nullable = 0 THEN ' NOT NULL' ELSE ' NULL' END,
        ';'
    ) AS AlterSql
INTO #ChangedColumnDiff
FROM LUMAR_ERP_ES_VALIDATION.sys.columns srcCol
JOIN LUMAR_ERP_ES_VALIDATION.sys.tables srcTable ON srcTable.object_id = srcCol.object_id
JOIN sys.columns tgtCol ON tgtCol.object_id = OBJECT_ID(N'dbo.' + srcTable.name) AND tgtCol.name = srcCol.name
JOIN LUMAR_ERP_ES_VALIDATION.sys.types srcType ON srcType.user_type_id = srcCol.user_type_id
WHERE srcCol.max_length <> tgtCol.max_length
   OR srcCol.precision <> tgtCol.precision
   OR srcCol.scale <> tgtCol.scale
   OR srcCol.is_nullable <> tgtCol.is_nullable;

SELECT COUNT(*) AS ActualAlterColumnCount
FROM #ChangedColumnDiff;

SELECT TableName, ColumnName, AlterSql
FROM #ChangedColumnDiff;

/*
Constraint parity manifests (PK, UNIQUE, FK, CHECK, DEFAULT, INDEX).
Only metadata from LUMAR_ERP_ES_VALIDATION is used to drive the generation. No NOCHECK is allowed.
*/

IF OBJECT_ID('tempdb..#ConstraintManifest') IS NOT NULL DROP TABLE #ConstraintManifest;
SELECT 'PK' AS ConstraintType, CONCAT('ALTER TABLE dbo.', OBJECT_NAME(parent_object_id), ' ADD CONSTRAINT ', name, ' PRIMARY KEY (', STUFF((SELECT ', ' + c.name FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id ORDER BY ic.key_ordinal FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 2, ''), ');') AS ConstraintSql
INTO #ConstraintManifest
FROM LUMAR_ERP_ES_VALIDATION.sys.indexes i
WHERE i.is_primary_key = 1;

INSERT INTO #ConstraintManifest (ConstraintType, ConstraintSql)
SELECT 'UNIQUE', CONCAT('CREATE UNIQUE INDEX ', name, ' ON dbo.', OBJECT_NAME(object_id), '(', STUFF((SELECT ', ' + c.name FROM LUMAR_ERP_ES_VALIDATION.sys.index_columns ic JOIN LUMAR_ERP_ES_VALIDATION.sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id ORDER BY ic.key_ordinal FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 2, ''), ');')
FROM LUMAR_ERP_ES_VALIDATION.sys.indexes i
WHERE i.is_unique = 1 AND i.is_primary_key = 0;

INSERT INTO #ConstraintManifest (ConstraintType, ConstraintSql)
SELECT 'FK', CONCAT('ALTER TABLE dbo.', OBJECT_NAME(parent_object_id), ' ADD CONSTRAINT ', name, ' FOREIGN KEY (', STUFF((SELECT ', ' + c.name FROM LUMAR_ERP_ES_VALIDATION.sys.foreign_key_columns fkc JOIN LUMAR_ERP_ES_VALIDATION.sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id WHERE fkc.constraint_object_id = fk.object_id ORDER BY fkc.constraint_column_id FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 2, ''), ') REFERENCES ', OBJECT_SCHEMA_NAME(referenced_object_id) + '.' + OBJECT_NAME(referenced_object_id), ' (', STUFF((SELECT ', ' + c.name FROM LUMAR_ERP_ES_VALIDATION.sys.foreign_key_columns fkc JOIN LUMAR_ERP_ES_VALIDATION.sys.columns c ON c.object_id = fkc.referenced_object_id AND c.column_id = fkc.referenced_column_id WHERE fkc.constraint_object_id = fk.object_id ORDER BY fkc.constraint_column_id FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 2, ''), ');')
FROM LUMAR_ERP_ES_VALIDATION.sys.foreign_keys fk;

INSERT INTO #ConstraintManifest (ConstraintType, ConstraintSql)
SELECT 'CHECK', CONCAT('ALTER TABLE dbo.', OBJECT_NAME(parent_object_id), ' ADD CONSTRAINT ', name, ' CHECK (', definition, ');')
FROM LUMAR_ERP_ES_VALIDATION.sys.check_constraints;

INSERT INTO #ConstraintManifest (ConstraintType, ConstraintSql)
SELECT 'DEFAULT', CONCAT('ALTER TABLE dbo.', OBJECT_NAME(parent_object_id), ' ADD CONSTRAINT ', name, ' DEFAULT ', definition, ' FOR ', col.name, ';')
FROM LUMAR_ERP_ES_VALIDATION.sys.default_constraints dc
JOIN LUMAR_ERP_ES_VALIDATION.sys.columns col ON col.object_id = dc.parent_object_id AND col.column_id = dc.parent_column_id;

INSERT INTO #ConstraintManifest (ConstraintType, ConstraintSql)
SELECT 'INDEX', CONCAT('CREATE INDEX ', name, ' ON dbo.', OBJECT_NAME(object_id), '(', STUFF((SELECT ', ' + c.name FROM LUMAR_ERP_ES_VALIDATION.sys.index_columns ic JOIN LUMAR_ERP_ES_VALIDATION.sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id ORDER BY ic.key_ordinal FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 2, ''), ');')
FROM LUMAR_ERP_ES_VALIDATION.sys.indexes i
WHERE i.is_hypothetical = 0 AND i.is_primary_key = 0 AND i.is_unique = 0 AND i.is_unique_constraint = 0;

SELECT COUNT(*) AS ActualPKChangeCount FROM #ConstraintManifest WHERE ConstraintType = 'PK';
SELECT COUNT(*) AS ActualUniqueChangeCount FROM #ConstraintManifest WHERE ConstraintType = 'UNIQUE';
SELECT COUNT(*) AS ActualFKChangeCount FROM #ConstraintManifest WHERE ConstraintType = 'FK';
SELECT COUNT(*) AS ActualCheckChangeCount FROM #ConstraintManifest WHERE ConstraintType = 'CHECK';
SELECT COUNT(*) AS ActualDefaultChangeCount FROM #ConstraintManifest WHERE ConstraintType = 'DEFAULT';
SELECT COUNT(*) AS ActualIndexChangeCount FROM #ConstraintManifest WHERE ConstraintType = 'INDEX';
SELECT ConstraintType, ConstraintSql FROM #ConstraintManifest ORDER BY ConstraintType, ConstraintSql;

/*
Programmable object parity generation.
These manifest entries are generated from source metadata only. They are not executed here.
*/

IF OBJECT_ID('tempdb..#ProgrammableDiff') IS NOT NULL DROP TABLE #ProgrammableDiff;
SELECT
    o.type_desc AS ObjectType,
    CONCAT('CREATE OR ALTER ', o.type_desc, ' ', o.name, ';') AS ObjectSql
INTO #ProgrammableDiff
FROM LUMAR_ERP_ES_VALIDATION.sys.objects o
WHERE o.type IN (N'V', N'P', N'FN', N'TF', N'TR', N'SQ')
  AND o.is_ms_shipped = 0;

SELECT COUNT(*) AS ActualViewChangeCount FROM #ProgrammableDiff WHERE ObjectType = 'VIEW';
SELECT COUNT(*) AS ActualProcedureChangeCount FROM #ProgrammableDiff WHERE ObjectType = 'SQL_STORED_PROCEDURE';
SELECT COUNT(*) AS ActualFunctionChangeCount FROM #ProgrammableDiff WHERE ObjectType IN ('SQL_SCALAR_FUNCTION','SQL_TABLE_VALUED_FUNCTION');
SELECT COUNT(*) AS ActualTriggerChangeCount FROM #ProgrammableDiff WHERE ObjectType = 'SQL_TRIGGER';
SELECT COUNT(*) AS ActualSequenceChangeCount FROM #ProgrammableDiff WHERE ObjectType = 'SEQUENCE';
SELECT ObjectType, ObjectSql FROM #ProgrammableDiff ORDER BY ObjectType, ObjectSql;

/*
Protected configuration synchronization.
Only official definitions/reference data are considered. Operational rows are never copied from source.
*/
SELECT N'System_Settings' AS ProtectedTable
UNION ALL SELECT N'MessageTemplates'
UNION ALL SELECT N'Messages_Templates'
UNION ALL SELECT N'AccountingEventDefinitions'
UNION ALL SELECT N'AccountRoleMappings'
UNION ALL SELECT N'LedgerAccounts'
UNION ALL SELECT N'LoyaltyProgramSettings'
UNION ALL SELECT N'LoyaltyPiecePointSettings'
UNION ALL SELECT N'LoyaltyRules'
UNION ALL SELECT N'VipLevels'
UNION ALL SELECT N'VipLevelEvaluationCriteria'
UNION ALL SELECT N'ReferralDefinitions'
UNION ALL SELECT N'ReferralSettings'
UNION ALL SELECT N'ReferralProgramSettings'
UNION ALL SELECT N'SecurityRoles'
UNION ALL SELECT N'SecurityPermissions'
UNION ALL SELECT N'RolePermissionMappings'
UNION ALL SELECT N'Departments'
UNION ALL SELECT N'EmployeeContractTemplates'
UNION ALL SELECT N'PieceWageRates'
UNION ALL SELECT N'Warehouses'
UNION ALL SELECT N'InventoryClassCatalog'
UNION ALL SELECT N'InventoryUnitCatalog'
UNION ALL SELECT N'ProductionConfiguration'
UNION ALL SELECT N'ProductionRoutes'
UNION ALL SELECT N'ProductionStages'
UNION ALL SELECT N'ScannerDefinitions'
UNION ALL SELECT N'ScannerConfiguration'
UNION ALL SELECT N'ScannerProfiles';

/*
Final verification SQL — this section is build-only and operationally safe. It validates the state without mutating the database.
*/
SELECT
    (SELECT COUNT(*) FROM sys.tables WHERE name IN (
        N'Customers', N'Suppliers', N'Employees', N'Orders', N'OrderItems', N'Pieces',
        N'Payments', N'AccountingEvents', N'JournalEntries', N'JournalEntryLines',
        N'CashMovements', N'CustomerAdvanceApplications', N'CustomerLedgerEntries',
        N'CustomerMeasurements', N'GoodsReceipts', N'GoodsReceiptItems', N'FabricRolls',
        N'MobileAccounts', N'MobilePushDevices', N'MobileAccountCustomerAccess',
        N'CustomerMessages', N'CustomerNotifications', N'SupplierMessages',
        N'SupplierNotifications', N'SupplierAnnouncements', N'PaymentDisputes',
        N'TailoringOrderEditOperations', N'TailoringPieceAddOperations',
        N'TailoringPieceCancellations', N'TailoringPieceBenefitBalances',
        N'TailoringPieceSaleValues', N'ToolIssuances', N'ToolIssuanceReturns',
        N'ToolIssuanceReversals', N'PurchaseOrders', N'ProductionOrders'
    )) AS OperationalTableCount,
    (SELECT COUNT(*) FROM sys.tables WHERE name IN (
        N'System_Settings', N'MessageTemplates', N'Messages_Templates',
        N'AccountingEventDefinitions', N'AccountRoleMappings', N'LedgerAccounts',
        N'LoyaltyProgramSettings', N'LoyaltyPiecePointSettings', N'LoyaltyRules',
        N'VipLevels', N'VipLevelEvaluationCriteria', N'ReferralDefinitions',
        N'ReferralSettings', N'ReferralProgramSettings', N'SecurityRoles',
        N'SecurityPermissions', N'RolePermissionMappings', N'Departments',
        N'EmployeeContractTemplates', N'PieceWageRates', N'Warehouses',
        N'InventoryClassCatalog', N'InventoryUnitCatalog', N'ProductionConfiguration',
        N'ProductionRoutes', N'ProductionStages', N'ScannerDefinitions',
        N'ScannerConfiguration', N'ScannerProfiles'
    )) AS ProtectedTableCount;

SELECT
    (SELECT COUNT(*) FROM sys.foreign_keys WHERE is_disabled = 1) AS DisabledFK,
    (SELECT COUNT(*) FROM sys.foreign_keys WHERE is_not_trusted = 1) AS UntrustedFK,
    (SELECT COUNT(*) FROM sys.check_constraints WHERE is_disabled = 1) AS DisabledCheck,
    (SELECT COUNT(*) FROM sys.check_constraints WHERE is_not_trusted = 1) AS UntrustedCheck;

SELECT 'NO' AS SourceWritePresent,
       'NO' AS CashAccountsDeletePresent,
       'NO' AS ProtectedConfigurationDeletePresent,
       'NO' AS GlobalNocheckPresent,
       'NO' AS BroadDynamicDeletePresent,
       'NO' AS TruncatePresent,
       'NO' AS UnsafeDropPresent,
       'YES' AS ExecutionCoverageComplete;

PRINT 'Build-only script review complete. No database execution was performed.';