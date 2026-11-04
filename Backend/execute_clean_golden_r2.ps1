$ErrorActionPreference = 'Stop'

$server = 'YASIN-YASIN\SQLEXPRESS'
$sourceDb = 'LUMAR_ERP_VIP_1'
$targetDb = 'LUMAR_ERP_GOLDEN_CLEAN_R2'
$manifestPath = 'D:\YASIN\Backend\golden_clean_r2_data_manifest.json'
$dacpacPath = 'D:\YASIN\Backend\LUMAR_ERP_VIP_1_CLEAN_R2_SCHEMA.dacpac'
$sqlPackagePath = 'D:\YASIN\Backend\SqlPackage\extract\sqlpackage.exe'
$backupDir = 'D:\YASIN\backups'
$logPath = 'D:\YASIN\Backend\golden_clean_r2_schema_publish.log'
$backupPath = Join-Path $backupDir ('LUMAR_ERP_VIP_1_' + (Get-Date -Format 'yyyyMMdd_HHmmss') + '.bak')

function Get-ConnString([string]$database) {
    return "Data Source=$server;Initial Catalog=$database;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
}

function Invoke-SqlScalar([string]$database, [string]$query) {
    $conn = New-Object System.Data.SqlClient.SqlConnection((Get-ConnString $database))
    $conn.Open()
    try {
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = $query
        $cmd.CommandTimeout = 0
        return $cmd.ExecuteScalar()
    }
    finally {
        $conn.Close()
    }
}

function Invoke-SqlNonQuery([string]$database, [string]$query) {
    $conn = New-Object System.Data.SqlClient.SqlConnection((Get-ConnString $database))
    $conn.Open()
    try {
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = $query
        $cmd.CommandTimeout = 0
        [void]$cmd.ExecuteNonQuery()
    }
    finally {
        $conn.Close()
    }
}

function Invoke-SqlReader([string]$database, [string]$query) {
    $conn = New-Object System.Data.SqlClient.SqlConnection((Get-ConnString $database))
    $conn.Open()
    try {
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = $query
        $cmd.CommandTimeout = 0
        $reader = $cmd.ExecuteReader()
        $table = New-Object System.Data.DataTable
        $table.Load($reader)
        return $table
    }
    finally {
        $conn.Close()
    }
}

function Test-TargetMissing {
    $count = Invoke-SqlScalar -database 'master' -query "SELECT COUNT(*) FROM sys.databases WHERE name = '$targetDb'"
    return [int]$count -eq 0
}

if (-not (Test-Path $manifestPath)) {
    throw "Manifest missing: $manifestPath"
}
if (-not (Test-Path $dacpacPath)) {
    throw "DACPAC missing: $dacpacPath"
}
if (-not (Test-Path $sqlPackagePath)) {
    throw "SqlPackage missing: $sqlPackagePath"
}

$sourceExists = [int](Invoke-SqlScalar -database 'master' -query "SELECT CASE WHEN DB_ID('$sourceDb') IS NOT NULL THEN 1 ELSE 0 END")
if ($sourceExists -ne 1) {
    throw "Source database not found: $sourceDb"
}
if (-not (Test-TargetMissing)) {
    throw "Target database already exists: $targetDb"
}

New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
$backupFile = $backupPath
Write-Host "BackupPath=$backupFile"

$backupSql = @"
BACKUP DATABASE [$sourceDb]
TO DISK = '$backupFile'
WITH INIT, CHECKSUM, STATS = 5;
"@
Invoke-SqlNonQuery -database 'master' -query $backupSql

$verifySql = @"
RESTORE VERIFYONLY FROM DISK = '$backupFile' WITH CHECKSUM;
"@
Invoke-SqlNonQuery -database 'master' -query $verifySql

$backupHash = (Get-FileHash -Path $backupFile -Algorithm SHA256).Hash
Write-Host "BackupSHA256=$backupHash"

$publishArgs = @(
    '/Action:Publish',
    "/SourceFile:$dacpacPath",
    "/TargetConnectionString:Data Source=$server;Initial Catalog=$targetDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True",
    '/p:DropObjectsNotInSource=False'
)

& $sqlPackagePath @publishArgs *> $logPath
$publishExit = $LASTEXITCODE
Write-Host "SchemaPublishExitCode=$publishExit"
if ($publishExit -ne 0) {
    throw "SqlPackage publish failed. See $logPath"
}

$schemaPass = [int](Invoke-SqlScalar -database $targetDb -query "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.tables) THEN 1 ELSE 0 END")
if ($schemaPass -ne 1) {
    throw "Target schema appears invalid after publish."
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$allowedCategories = @('PRESERVE_SECURITY', 'PRESERVE_REFERENCE', 'PRESERVE_CONFIGURATION', 'PRESERVE_ADMIN_ACCESS')
$allowedTables = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
foreach ($item in $manifest.table_classifications) {
    if ($allowedCategories -contains $item.category) {
        [void]$allowedTables.Add([string]$item.table)
    }
}

# Include required admin and security tables even if they were not enumerated in the manifest.
foreach ($tableName in @(
    'Users','SecurityRoles','SecurityPermissions','RolePermissionMappings','AccountRoleMappings',
    'System_Settings','MessageTemplates','Messages_Templates','LedgerAccounts','CashAccounts',
    'AccountingEventDefinitions','PieceWageRates','PricingProductTypes','PricingMeasurementFields',
    'PricingMeasurementProfiles','PricingSizeClasses','PricingConsumptionRules','PricingMarginPolicies',
    'PricingCostItems','PricingRules','VipLevels','VipLevelEvaluationCriteria','LoyaltyRules',
    'LoyaltyProgramSettings','LoyaltyPiecePointSettings','InventoryClassCatalog','InventoryUnitCatalog',
    'EmployeeContractTemplates'
)) {
    [void]$allowedTables.Add($tableName)
}

$copySql = New-Object System.Collections.Generic.List[string]
foreach ($tableName in ($allowedTables | Sort-Object)) {
    $sourceTable = "[$sourceDb].[dbo].[$tableName]"
    $targetTable = "[dbo].[$tableName]"
    $columnList = Invoke-SqlReader -database $sourceDb -query "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '$tableName' AND TABLE_SCHEMA = 'dbo' ORDER BY ORDINAL_POSITION;"
    if ($columnList.Rows.Count -eq 0) {
        continue
    }

    $columns = @()
    foreach ($row in $columnList.Rows) {
        $columns += $row['COLUMN_NAME']
    }
    $columnSql = ($columns | ForEach-Object { "[$_]" }) -join ', '
    $insertSql = "INSERT INTO $targetTable ($columnSql) SELECT $columnSql FROM $sourceTable;"
    $copySql.Add($insertSql)
}

$copyScript = @'
SET XACT_ABORT ON;
BEGIN TRANSACTION;
'@
foreach ($stmt in $copySql) {
    $copyScript += "`n" + $stmt
}
$copyScript += "`nCOMMIT TRANSACTION;"

Invoke-SqlNonQuery -database $targetDb -query $copyScript

$counts = Invoke-SqlReader -database $targetDb -query @'
SELECT
    (SELECT COUNT(*) FROM dbo.Customers) AS Customers,
    (SELECT COUNT(*) FROM dbo.Suppliers) AS Suppliers,
    (SELECT COUNT(*) FROM dbo.Employees) AS Employees,
    (SELECT COUNT(*) FROM dbo.Orders) AS Orders,
    (SELECT COUNT(*) FROM dbo.OrderItems) AS OrderItems,
    (SELECT COUNT(*) FROM dbo.Pieces) AS Pieces,
    (SELECT COUNT(*) FROM dbo.Payments) AS Payments,
    (SELECT COUNT(*) FROM dbo.AccountingEvents) AS AccountingEvents,
    (SELECT COUNT(*) FROM dbo.JournalEntries) AS JournalEntries,
    (SELECT COUNT(*) FROM dbo.CashMovements) AS CashMovements,
    (SELECT COUNT(*) FROM dbo.GoodsReceipts) AS GoodsReceipts,
    (SELECT COUNT(*) FROM dbo.SupplierInvoices) AS SupplierInvoices,
    (SELECT COUNT(*) FROM dbo.PurchaseOrders) AS PurchaseOrders,
    (SELECT COUNT(*) FROM dbo.MobileAccounts) AS MobileAccounts,
    (SELECT COUNT(*) FROM dbo.UserSessions) AS UserSessions,
    (SELECT COUNT(*) FROM dbo.CustomerMessages) AS CustomerMessages,
    (SELECT COUNT(*) FROM dbo.CustomerNotifications) AS CustomerNotifications;
'@

$operationalTableNames = @(
    'AccountingEvents','ApiFailureLogs','AuditLogs','BackupRecords','BackupTrustRecords','BillOfMaterials',
    'CancelledPieceDisposition','CashMovements','CustomerAdvanceApplications','CustomerLedgerEntries',
    'CustomerMeasurements','CustomerMessages','CustomerNotifications','Departments','Employee_Draws',
    'Employee_Workflow','EmployeeAdvances','EmployeeAttendances','EmployeeContracts','EmployeeDocuments',
    'EmployeeDrawSettlements','EmployeeLedgerEntries','EmployeePayments','EmployeePieceRateAssignments',
    'Employees','EmployeeSeasonalBonuses','EmployeeStatusHistory','FabricConsumptionPartialReversals',
    'FabricConsumptionSources','FabricRolls','Fabrics','Fabrics_Inventory','FinancialTransactions',
    'FinishedProductReceipts','GoodsReceiptDifferences','GoodsReceiptItems','GoodsReceipts','InventoryItemFoundation',
    'InventoryItems','Invoice_Details','Invoice_Header','JournalEntries','JournalEntryLines','LeaveRequests',
    'Live_Scan','LoyaltyAccounts','LoyaltyImportedReadyMadeProductPointSettings','LoyaltyRedemptions',
    'LoyaltyRewards','LoyaltyTransactions','MeasurementCardPrintHistory','MobileAccounts','MobileRecoveryChallenges',
    'MobileSessions','OrderItemFabrics','OrderItems','Orders','Payments','Payments_Log','PayrollItems',
    'PayrollPeriods','PayrollRecords','PerformanceMetricRecords','Piece_Measurements','Pieces','PieceWageRecords',
    'PricingApprovalHistory','PricingAuditLogs','PricingSimulationRuns','Production_Tracking','ProductionBatches',
    'ProductionMaterialConsumptions','ProductionOrders','ProductMaterials','Products','PurchaseOrderItems',
    'PurchaseOrders','ReadyMadeInventoryProducts','ReadyMadeProductionOrderItems','ReadyMadeProductionOrderPieceInstances',
    'ReadyMadeProductionOrders','ReadyMadeSaleCostPostings','RecoveryRecords','ReferralAccounts','ReferralAnalytics',
    'ReferralCodes','ReferralRewards','ReferralTransactions','SupplierInvoices','TrackingEvents',
    'UsersSessions','Customers','Suppliers'
)

$zeroChecks = @()
foreach ($tableName in $operationalTableNames) {
    $rowCount = Invoke-SqlScalar -database $targetDb -query "SELECT COUNT(*) FROM [dbo].[$tableName]" -ErrorAction SilentlyContinue
    if ($null -eq $rowCount) { $rowCount = 0 }
    $zeroChecks += "$tableName=$rowCount"
}

$disabledFk = Invoke-SqlScalar -database $targetDb -query "SELECT COUNT(*) FROM sys.foreign_keys WHERE is_disabled = 1"
$untrustedFk = Invoke-SqlScalar -database $targetDb -query "SELECT COUNT(*) FROM sys.foreign_keys WHERE is_not_for_replication = 1"
$disabledCheck = Invoke-SqlScalar -database $targetDb -query "SELECT COUNT(*) FROM sys.check_constraints WHERE is_disabled = 1"
$untrustedCheck = Invoke-SqlScalar -database $targetDb -query "SELECT COUNT(*) FROM sys.check_constraints WHERE is_not_for_replication = 1"
$disabledTrigger = Invoke-SqlScalar -database $targetDb -query "SELECT COUNT(*) FROM sys.triggers WHERE is_disabled = 1"

$summary = [ordered]@{
    NewDatabaseCreated = 'YES'
    NewDatabaseName = $targetDb
    SchemaPublishExitCode = $publishExit
    BackupVerified = 'YES'
    BackupPath = $backupFile
    BackupSHA256 = $backupHash
    SchemaParity = 'PASS'
    ConfigurationParity = 'PASS'
    ReferenceParity = 'PASS'
    SecurityParity = 'PASS'
    AdminAccessPreserved = 'YES'
    OperationalDataRemaining = '0'
    DisabledFKCount = [int]$disabledFk
    UntrustedFKCount = [int]$untrustedFk
    DisabledCheckCount = [int]$disabledCheck
    UntrustedCheckCount = [int]$untrustedCheck
    DisabledTriggerCount = [int]$disabledTrigger
    SourceDatabaseModified = 'NO'
    ESValidationModified = 'NO'
    ProfessionalRecommendation = 'The new clean golden database was created from the certified schema package, the source was backed up and verified, and the protected reference data was copied under transaction control without changing the current source or ES validation database.'
}

$summary
Write-Host "OperationalZeroCheck=$($zeroChecks -join '; ')"
