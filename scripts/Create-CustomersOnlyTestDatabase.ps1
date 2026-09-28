[CmdletBinding()]
param(
    [string]$ServerInstance = 'YASIN-YASIN\SQLEXPRESS',
    [string]$SourceDatabase = 'LUMAR_ERP_TEST',
    [string]$TargetDatabase = 'LUMAR_ERP_CUSTOMERS_ONLY_TEST'
)

$ErrorActionPreference = 'Stop'

function New-DatabaseConnection {
    param([string]$Database)

    $connection = New-Object System.Data.SqlClient.SqlConnection
    $connection.ConnectionString = "Server=$ServerInstance;Database=$Database;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
    $connection.Open()
    return $connection
}

function Invoke-DatabaseNonQuery {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [string]$Sql,
        [int]$TimeoutSeconds = 120
    )

    $command = $Connection.CreateCommand()
    $command.CommandText = $Sql
    $command.CommandTimeout = $TimeoutSeconds
    try {
        [void]$command.ExecuteNonQuery()
    }
    finally {
        $command.Dispose()
    }
}

function Invoke-DatabaseTable {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [string]$Sql,
        [int]$TimeoutSeconds = 120
    )

    $command = $Connection.CreateCommand()
    $command.CommandText = $Sql
    $command.CommandTimeout = $TimeoutSeconds
    $table = New-Object System.Data.DataTable
    try {
        $reader = $command.ExecuteReader()
        try {
            $table.Load($reader)
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $command.Dispose()
    }

    return ,$table
}

function ConvertTo-SqlLiteral {
    param([string]$Value)

    return "N'" + $Value.Replace("'", "''") + "'"
}

function Get-TableRowCounts {
    param([System.Data.SqlClient.SqlConnection]$Connection)

    return Invoke-DatabaseTable -Connection $Connection -Sql @'
SELECT t.name AS TableName, SUM(p.rows) AS TotalRows
FROM sys.tables AS t
LEFT JOIN sys.partitions AS p
    ON p.object_id = t.object_id
    AND p.index_id IN (0, 1)
GROUP BY t.name
ORDER BY t.name;
'@
}

function Get-CustomerProof {
    param([System.Data.SqlClient.SqlConnection]$Connection)

    return Invoke-DatabaseTable -Connection $Connection -Sql @'
DECLARE @payload nvarchar(max) =
(
    SELECT STRING_AGG(
        CAST(CONCAT(CustomerID, N'|', ISNULL(CustomerCode, N''), N'|',
            ISNULL(CustomerName, N''), N'|', ISNULL(PhoneNumber, N'')) AS nvarchar(max)),
        NCHAR(10)) WITHIN GROUP (ORDER BY CustomerID)
    FROM dbo.Customers
);

SELECT
    COUNT(*) AS CustomerCount,
    MIN(CustomerID) AS MinCustomerId,
    MAX(CustomerID) AS MaxCustomerId,
    SUM(CASE WHEN CustomerCode IS NULL OR LTRIM(RTRIM(CustomerCode)) = N'' THEN 1 ELSE 0 END) AS BlankCustomerCodes,
    COUNT(*) - COUNT(DISTINCT CustomerCode) AS DuplicateCustomerCodes,
    SUM(CASE WHEN COALESCE(TotalPoints, 0) <> 0 OR COALESCE(TotalPieces, 0) <> 0 OR COALESCE(TotalDebts, 0) <> 0 THEN 1 ELSE 0 END) AS CustomersWithNonZeroSnapshots,
    CONVERT(varchar(64), HASHBYTES('SHA2_256', CONVERT(varbinary(max), @payload)), 2) AS CustomerSha256
FROM dbo.Customers;
'@
}

$knownTables = @(
    '__EFMigrationsHistory', 'AccountingEvents', 'ApiFailureLogs', 'AuditLogs', 'BackupRecords',
    'BackupTrustRecords', 'BillOfMaterials', 'CancelledPieceDisposition', 'CashAccounts',
    'CashMovements', 'CustomerAdvanceApplications', 'CustomerLedgerEntries', 'CustomerMeasurements',
    'CustomerMessages', 'CustomerNotifications', 'Customers', 'Departments', 'Employee_Draws',
    'Employee_Workflow', 'EmployeeAttendances', 'EmployeeContracts', 'EmployeeContractTemplates',
    'EmployeeDocuments', 'EmployeeDrawSettlements', 'EmployeePieceRateAssignments', 'Employees',
    'FabricConsumptionSources', 'FabricRolls', 'Fabrics', 'Fabrics_Inventory', 'FinancialTransactions',
    'FinishedProductReceipts', 'GoodsReceiptItems', 'GoodsReceipts', 'ImportedReadyMadeInventoryReceipts',
    'ImportedReadyMadeProducts', 'ImportedReadyMadeSaleCostPostings', 'InventoryClassCatalog',
    'InventoryFoundationProcedureBackups', 'InventoryItemFoundation', 'InventoryItems',
    'InventoryReceiptLines', 'InventoryReceiptPostings', 'InventoryTransactions', 'InventoryUnitCatalog',
    'InventoryValuationSnapshots', 'Invoice_Details', 'Invoice_Header', 'JournalEntries',
    'JournalEntryLines', 'LeaveRequests', 'LedgerAccounts', 'Live_Scan', 'LoyaltyAccounts',
    'LoyaltyImportedReadyMadeProductPointSettings', 'LoyaltyPiecePointSettings',
    'LoyaltyProgramSettings', 'LoyaltyReadyMadeProductTypePointSettings', 'LoyaltyRedemptions',
    'LoyaltyRewards', 'LoyaltyRules', 'LoyaltyTransactions', 'MeasurementCardPrintHistory',
    'Messages_Templates', 'MessageTemplates', 'MobileAccounts', 'MobileRecoveryChallenges',
    'MobileSessions', 'OrderItemFabrics', 'OrderItems', 'Orders', 'Payments', 'Payments_Log',
    'PayrollItems', 'PayrollPeriods', 'PayrollRecords', 'PerformanceMetricRecords',
    'Piece_Measurements', 'Piece_Rates', 'Pieces', 'PieceWageRates', 'PieceWageRecords',
    'PricingApprovalHistory', 'PricingAuditLogs', 'PricingConsumptionRules', 'PricingCostItems',
    'PricingCostMatrices', 'PricingCostMatrixItems', 'PricingMarginPolicies',
    'PricingMeasurementFields', 'PricingMeasurementProfiles', 'PricingProductTypes', 'PricingRules',
    'PricingSimulationRuns', 'PricingSizeClasses', 'PricingWastePolicies', 'Production_Tracking',
    'ProductionBatches', 'ProductionMaterialConsumptions', 'ProductionOrders', 'ProductMaterials',
    'Products', 'PurchaseOrderItems', 'PurchaseOrders', 'ReadyMadeInventoryProducts',
    'ReadyMadeProductionOrderItems', 'ReadyMadeProductionOrderPieceInstances',
    'ReadyMadeProductionOrders', 'ReadyMadeSaleCostPostings', 'RecoveryRecords', 'ReferralAccounts',
    'ReferralAnalytics', 'ReferralCodes', 'ReferralRewards', 'ReferralTransactions', 'RestoreRecords',
    'RolePermissionMappings', 'Scanners', 'SecurityDriftRecords', 'SecurityEventLogs',
    'SecurityPermissions', 'SecurityRoles', 'Sent_Log', 'Sent_Messages', 'ServiceAvailabilityRecords',
    'SupplierInvoices', 'SupplierLedgerEntries', 'SupplierPaymentAllocations', 'SupplierPayments',
    'Suppliers', 'SupplierTransactions', 'sysdiagrams', 'System_Settings', 'SystemHealthRecords',
    'TrackingEvents', 'UserActivityLogs', 'UserClaimMappings', 'UserRoleAssignments', 'Users',
    'UserSessions', 'VipLevelEvaluationCriteria', 'VipLevels'
)

$preserveSecurity = @(
    'RolePermissionMappings', 'SecurityPermissions', 'SecurityRoles', 'UserClaimMappings',
    'UserRoleAssignments', 'Users'
)

$preserveReference = @(
    '__EFMigrationsHistory', 'CashAccounts', 'EmployeeContractTemplates', 'InventoryClassCatalog',
    'InventoryUnitCatalog', 'LedgerAccounts', 'LoyaltyPiecePointSettings', 'LoyaltyProgramSettings',
    'LoyaltyReadyMadeProductTypePointSettings', 'LoyaltyRules', 'Messages_Templates',
    'MessageTemplates', 'Piece_Rates', 'PieceWageRates', 'PricingConsumptionRules',
    'PricingCostItems', 'PricingCostMatrices', 'PricingCostMatrixItems', 'PricingMarginPolicies',
    'PricingMeasurementFields', 'PricingMeasurementProfiles', 'PricingProductTypes', 'PricingRules',
    'PricingSizeClasses', 'PricingWastePolicies', 'Scanners', 'sysdiagrams', 'System_Settings',
    'VipLevelEvaluationCriteria', 'VipLevels'
)

$preserveCustomers = @('Customers')
$emptyOperational = @($knownTables | Where-Object {
    $_ -notin $preserveSecurity -and $_ -notin $preserveReference -and $_ -notin $preserveCustomers
})

$masterConnection = New-DatabaseConnection -Database 'master'
$sourceConnection = $null
$targetConnection = $null

try {
    $sourceConnection = New-DatabaseConnection -Database $SourceDatabase

    $actualTableData = Invoke-DatabaseTable -Connection $sourceConnection -Sql 'SELECT name AS TableName FROM sys.tables ORDER BY name;'
    $actualTables = @($actualTableData.Rows | ForEach-Object { $_.TableName })
    $missingClassifications = @($actualTables | Where-Object { $_ -notin $knownTables })
    $staleClassifications = @($knownTables | Where-Object { $_ -notin $actualTables })
    $duplicateClassifications = @($knownTables | Group-Object | Where-Object { $_.Count -ne 1 } | ForEach-Object Name)

    if ($missingClassifications.Count -gt 0 -or $staleClassifications.Count -gt 0 -or $duplicateClassifications.Count -gt 0) {
        throw "MANIFEST_NOT_PROVEN Missing=[$($missingClassifications -join ',')] Stale=[$($staleClassifications -join ',')] Duplicate=[$($duplicateClassifications -join ',')]"
    }

    $targetExists = Invoke-DatabaseTable -Connection $masterConnection -Sql "SELECT DB_ID($(ConvertTo-SqlLiteral $TargetDatabase)) AS TargetDatabaseId;"
    if ($targetExists.Rows[0].IsNull('TargetDatabaseId') -eq $false) {
        throw "TARGET_DATABASE_ALREADY_EXISTS: $TargetDatabase"
    }

    $sourceProofBefore = Get-CustomerProof -Connection $sourceConnection
    $sourceRowsBefore = Get-TableRowCounts -Connection $sourceConnection
    $paths = Invoke-DatabaseTable -Connection $masterConnection -Sql "SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(4000)) AS BackupPath, CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(4000)) AS DataPath, CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(4000)) AS LogPath;"
    $sourceFiles = Invoke-DatabaseTable -Connection $masterConnection -Sql "SELECT name AS LogicalName, type_desc AS FileType FROM sys.master_files WHERE database_id = DB_ID($(ConvertTo-SqlLiteral $SourceDatabase)) ORDER BY file_id;"

    if ($sourceFiles.Rows.Count -ne 2) {
        throw "SOURCE_FILE_LAYOUT_NOT_PROVEN: expected 2 files, found $($sourceFiles.Rows.Count)"
    }

    $backupPath = $paths.Rows[0].BackupPath
    $dataPath = $paths.Rows[0].DataPath
    $logPath = $paths.Rows[0].LogPath
    $timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $backupFile = Join-Path $backupPath "$SourceDatabase`_CUSTOMERS_ONLY_$timestamp.bak"
    $targetMdf = Join-Path $dataPath "$TargetDatabase.mdf"
    $targetLdf = Join-Path $logPath "$TargetDatabase`_log.ldf"

    if ((Test-Path $backupFile) -or (Test-Path $targetMdf) -or (Test-Path $targetLdf)) {
        throw 'TARGET_OR_BACKUP_FILE_ALREADY_EXISTS'
    }

    $dataLogicalName = ($sourceFiles.Rows | Where-Object { $_.FileType -eq 'ROWS' }).LogicalName
    $logLogicalName = ($sourceFiles.Rows | Where-Object { $_.FileType -eq 'LOG' }).LogicalName
    if ([string]::IsNullOrWhiteSpace($dataLogicalName) -or [string]::IsNullOrWhiteSpace($logLogicalName)) {
        throw 'SOURCE_LOGICAL_FILE_NAMES_NOT_PROVEN'
    }

    Write-Output "SOURCE_DATABASE=$SourceDatabase"
    Write-Output "TARGET_DATABASE=$TargetDatabase"
    Write-Output "MANIFEST_COUNTS=PRESERVE_SECURITY:$($preserveSecurity.Count);PRESERVE_REFERENCE:$($preserveReference.Count);PRESERVE_CUSTOMERS:$($preserveCustomers.Count);EMPTY_OPERATIONAL:$($emptyOperational.Count);NOT_PROVEN:0"
    Write-Output "BACKUP_FILE=$backupFile"

    Invoke-DatabaseNonQuery -Connection $masterConnection -TimeoutSeconds 1800 -Sql "BACKUP DATABASE [$SourceDatabase] TO DISK = $(ConvertTo-SqlLiteral $backupFile) WITH COPY_ONLY, CHECKSUM, STATS = 5;"
    Invoke-DatabaseNonQuery -Connection $masterConnection -TimeoutSeconds 600 -Sql "RESTORE VERIFYONLY FROM DISK = $(ConvertTo-SqlLiteral $backupFile) WITH CHECKSUM;"
    $backupEvidence = Invoke-DatabaseTable -Connection $masterConnection -Sql "RESTORE HEADERONLY FROM DISK = $(ConvertTo-SqlLiteral $backupFile);"
    $backupHeader = $backupEvidence.Rows[0]
    Write-Output "BACKUP_EVIDENCE=DatabaseName:$($backupHeader.DatabaseName);Start:$($backupHeader.BackupStartDate.ToString('o'));Finish:$($backupHeader.BackupFinishDate.ToString('o'));Size:$($backupHeader.BackupSize);Type:$($backupHeader.BackupType);PhysicalDeviceName:$backupFile"
    Write-Output 'RESTORE_VERIFYONLY=PASSED'

    Invoke-DatabaseNonQuery -Connection $masterConnection -TimeoutSeconds 1800 -Sql "RESTORE DATABASE [$TargetDatabase] FROM DISK = $(ConvertTo-SqlLiteral $backupFile) WITH MOVE $(ConvertTo-SqlLiteral $dataLogicalName) TO $(ConvertTo-SqlLiteral $targetMdf), MOVE $(ConvertTo-SqlLiteral $logLogicalName) TO $(ConvertTo-SqlLiteral $targetLdf), RECOVERY, CHECKSUM, STATS = 5;"

    $targetConnection = New-DatabaseConnection -Database $TargetDatabase
    $manifestValues = foreach ($tableName in $knownTables) {
        $classification = if ($tableName -in $preserveSecurity) { 'PRESERVE_SECURITY' } elseif ($tableName -in $preserveReference) { 'PRESERVE_REFERENCE' } elseif ($tableName -in $preserveCustomers) { 'PRESERVE_CUSTOMERS' } else { 'EMPTY_OPERATIONAL' }
        "($(ConvertTo-SqlLiteral $tableName), $(ConvertTo-SqlLiteral $classification))"
    }
    $manifestInsert = $manifestValues -join ",`n    "

    $cleanupSql = @"
SET XACT_ABORT ON;
IF DB_NAME() <> N'$TargetDatabase'
    THROW 51000, 'TARGET_DATABASE_GUARD_FAILED', 1;

CREATE TABLE #Manifest (TableName sysname NOT NULL PRIMARY KEY, Classification varchar(40) NOT NULL);
INSERT INTO #Manifest (TableName, Classification) VALUES
    $manifestInsert;

IF EXISTS (SELECT name FROM sys.tables EXCEPT SELECT TableName FROM #Manifest)
    THROW 51001, 'MANIFEST_NOT_PROVEN_SOURCE_TABLE_MISSING', 1;
IF EXISTS (SELECT TableName FROM #Manifest EXCEPT SELECT name FROM sys.tables)
    THROW 51002, 'MANIFEST_NOT_PROVEN_STALE_TABLE', 1;
IF EXISTS (SELECT 1 FROM #Manifest WHERE Classification = 'NOT_PROVEN')
    THROW 51003, 'MANIFEST_NOT_PROVEN', 1;

BEGIN TRANSACTION;

DECLARE @TableName sysname, @Sql nvarchar(max), @Rows bigint;
DECLARE allTables CURSOR LOCAL FAST_FORWARD FOR
    SELECT TableName FROM #Manifest ORDER BY TableName;
OPEN allTables;
FETCH NEXT FROM allTables INTO @TableName;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Sql = N'ALTER TABLE dbo.' + QUOTENAME(@TableName) + N' NOCHECK CONSTRAINT ALL; ALTER TABLE dbo.' + QUOTENAME(@TableName) + N' DISABLE TRIGGER ALL;';
    EXEC sys.sp_executesql @Sql;
    FETCH NEXT FROM allTables INTO @TableName;
END
CLOSE allTables;
DEALLOCATE allTables;

DECLARE emptyTables CURSOR LOCAL FAST_FORWARD FOR
    SELECT TableName FROM #Manifest WHERE Classification = 'EMPTY_OPERATIONAL' ORDER BY TableName;
OPEN emptyTables;
FETCH NEXT FROM emptyTables INTO @TableName;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Sql = N'DELETE FROM dbo.' + QUOTENAME(@TableName) + N';';
    EXEC sys.sp_executesql @Sql;
    FETCH NEXT FROM emptyTables INTO @TableName;
END
CLOSE emptyTables;
DEALLOCATE emptyTables;

UPDATE dbo.Customers
SET TotalPoints = 0,
    TotalPieces = 0,
    TotalDebts = 0,
    ParentCustomerCode = NULL,
    RelationshipType = NULL,
    ParentCustomerId = NULL;

UPDATE dbo.CashAccounts
SET CurrentBalance = 0;

DECLARE @Seed decimal(38, 0), @Increment decimal(38, 0), @Reseed decimal(38, 0);
DECLARE identityTables CURSOR LOCAL FAST_FORWARD FOR
    SELECT m.TableName
    FROM #Manifest AS m
    JOIN sys.tables AS t ON t.name = m.TableName
    JOIN sys.identity_columns AS ic ON ic.object_id = t.object_id
    WHERE m.Classification = 'EMPTY_OPERATIONAL'
    ORDER BY m.TableName;
OPEN identityTables;
FETCH NEXT FROM identityTables INTO @TableName;
WHILE @@FETCH_STATUS = 0
BEGIN
    SELECT @Seed = CONVERT(decimal(38, 0), ic.seed_value), @Increment = CONVERT(decimal(38, 0), ic.increment_value)
    FROM sys.tables AS t
    JOIN sys.identity_columns AS ic ON ic.object_id = t.object_id
    WHERE t.name = @TableName;
    SET @Reseed = @Seed - @Increment;
    SET @Sql = N'DBCC CHECKIDENT (''dbo.' + REPLACE(@TableName, '''', '''''') + N''', RESEED, ' + CONVERT(nvarchar(40), @Reseed) + N') WITH NO_INFOMSGS;';
    EXEC sys.sp_executesql @Sql;
    FETCH NEXT FROM identityTables INTO @TableName;
END
CLOSE identityTables;
DEALLOCATE identityTables;

DECLARE checkedTables CURSOR LOCAL FAST_FORWARD FOR
    SELECT TableName FROM #Manifest ORDER BY TableName;
OPEN checkedTables;
FETCH NEXT FROM checkedTables INTO @TableName;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Sql = N'ALTER TABLE dbo.' + QUOTENAME(@TableName) + N' WITH CHECK CHECK CONSTRAINT ALL; ALTER TABLE dbo.' + QUOTENAME(@TableName) + N' ENABLE TRIGGER ALL;';
    EXEC sys.sp_executesql @Sql;
    FETCH NEXT FROM checkedTables INTO @TableName;
END
CLOSE checkedTables;
DEALLOCATE checkedTables;

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE is_disabled = 1 OR is_not_trusted = 1)
    THROW 51004, 'FOREIGN_KEY_RECHECK_FAILED', 1;
"@

    Invoke-DatabaseNonQuery -Connection $targetConnection -TimeoutSeconds 1800 -Sql $cleanupSql

    try {
        $constraintViolations = Invoke-DatabaseTable -Connection $targetConnection -TimeoutSeconds 600 -Sql 'DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS;'
        if ($constraintViolations.Rows.Count -gt 0) {
            throw "CHECKCONSTRAINTS_FAILED: $($constraintViolations.Rows.Count) violation(s)"
        }

        Invoke-DatabaseNonQuery -Connection $targetConnection -Sql 'COMMIT TRANSACTION;'
    }
    catch {
        Invoke-DatabaseNonQuery -Connection $targetConnection -Sql 'IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;'
        throw
    }

    $targetProofAfter = Get-CustomerProof -Connection $targetConnection
    $targetRowsAfter = Get-TableRowCounts -Connection $targetConnection
    $sourceProofAfter = Get-CustomerProof -Connection $sourceConnection
    $sourceRowsAfter = Get-TableRowCounts -Connection $sourceConnection

    $sourceProofBefore.Rows[0] | Format-List | Out-String | Write-Output
    $targetProofAfter.Rows[0] | Format-List | Out-String | Write-Output
    $sourceProofAfter.Rows[0] | Format-List | Out-String | Write-Output

    foreach ($tableName in $emptyOperational) {
        $sourceBefore = [int64]($sourceRowsBefore.Rows | Where-Object TableName -eq $tableName | Select-Object -ExpandProperty TotalRows)
        $targetAfter = [int64]($targetRowsAfter.Rows | Where-Object TableName -eq $tableName | Select-Object -ExpandProperty TotalRows)
        if ($targetAfter -ne 0) {
            throw "OPERATIONAL_TABLE_NOT_EMPTY: $tableName=$targetAfter"
        }
        Write-Output "EMPTIED_TABLE=$tableName;DeletedRows=$sourceBefore;TargetRows=$targetAfter"
    }

    if ($sourceProofBefore.Rows[0].CustomerSha256 -ne $sourceProofAfter.Rows[0].CustomerSha256) {
        throw 'SOURCE_CUSTOMER_HASH_CHANGED'
    }

    $sourceChangedTables = @()
    foreach ($tableName in $knownTables) {
        $beforeCount = [int64]($sourceRowsBefore.Rows | Where-Object TableName -eq $tableName | Select-Object -ExpandProperty TotalRows)
        $afterCount = [int64]($sourceRowsAfter.Rows | Where-Object TableName -eq $tableName | Select-Object -ExpandProperty TotalRows)
        if ($beforeCount -ne $afterCount) {
            $sourceChangedTables += $tableName
        }
    }
    if ($sourceChangedTables.Count -gt 0) {
        throw "SOURCE_ROW_COUNTS_CHANGED: $($sourceChangedTables -join ',')"
    }

    $schemaCounts = Invoke-DatabaseTable -Connection $targetConnection -Sql @'
SELECT 'Tables' AS ObjectType, COUNT(*) AS ObjectCount FROM sys.tables
UNION ALL SELECT 'Views', COUNT(*) FROM sys.views
UNION ALL SELECT 'Procedures', COUNT(*) FROM sys.procedures
UNION ALL SELECT 'Functions', COUNT(*) FROM sys.objects WHERE type IN ('FN', 'IF', 'TF', 'FS', 'FT')
UNION ALL SELECT 'ForeignKeys', COUNT(*) FROM sys.foreign_keys;
'@
    $schemaCounts | Format-Table -AutoSize | Out-String | Write-Output
    Write-Output 'CLEAN_BASELINE_DATABASE_READY_FOR_VALIDATION_CLONE'
}
finally {
    if ($targetConnection) { $targetConnection.Dispose() }
    if ($sourceConnection) { $sourceConnection.Dispose() }
    if ($masterConnection) { $masterConnection.Dispose() }
}