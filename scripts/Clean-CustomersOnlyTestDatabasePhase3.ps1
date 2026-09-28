[CmdletBinding()]
param(
    [string]$ServerInstance = 'YASIN-YASIN\SQLEXPRESS',
    [string]$SourceDatabase = 'LUMAR_ERP_TEST',
    [string]$TargetDatabase = 'LUMAR_ERP_CUSTOMERS_ONLY_TEST',
    [string]$BackupDirectory = 'D:\YASIN\backups',
    [string]$ManifestPath = 'D:\YASIN\scripts\CustomersOnlyPhase2Manifest.json',
    [switch]$DiagnosticRollbackOnly
)

$ErrorActionPreference = 'Stop'
$expectedCustomerHash = '7DD643228BCDF820C4C0E990D163FF433D4FF68A576F2CCA766E5A5FE18F6B7E'

function New-DatabaseConnection {
    param([string]$Database)

    $connection = New-Object System.Data.SqlClient.SqlConnection
    $connection.ConnectionString = "Server=$ServerInstance;Database=$Database;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=False"
    $connection.Open()
    return $connection
}

function Invoke-DatabaseNonQuery {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [string]$Sql,
        [int]$TimeoutSeconds = 120,
        [System.Data.SqlClient.SqlTransaction]$Transaction = $null
    )

    $command = $Connection.CreateCommand()
    $command.CommandText = $Sql
    $command.CommandTimeout = $TimeoutSeconds
    if ($Transaction) {
        $command.Transaction = $Transaction
    }
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
        [int]$TimeoutSeconds = 120,
        [System.Data.SqlClient.SqlTransaction]$Transaction = $null
    )

    $command = $Connection.CreateCommand()
    $command.CommandText = $Sql
    $command.CommandTimeout = $TimeoutSeconds
    if ($Transaction) {
        $command.Transaction = $Transaction
    }
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

function Get-CustomerProof {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [System.Data.SqlClient.SqlTransaction]$Transaction = $null
    )

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
    CONVERT(varchar(64), HASHBYTES('SHA2_256', CONVERT(varbinary(max), @payload)), 2) AS CustomerSha256,
    SUM(CASE WHEN COALESCE(TotalPoints, 0) <> 0 OR COALESCE(TotalPieces, 0) <> 0 OR COALESCE(TotalDebts, 0) <> 0 THEN 1 ELSE 0 END) AS CustomersWithNonZeroSnapshots
FROM dbo.Customers;
'@ -Transaction $Transaction
}

function Get-TableRowCounts {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [System.Data.SqlClient.SqlTransaction]$Transaction = $null
    )

    return Invoke-DatabaseTable -Connection $Connection -Sql @'
SELECT t.name AS TableName, SUM(p.rows) AS TotalRows
FROM sys.tables AS t
LEFT JOIN sys.partitions AS p
    ON p.object_id = t.object_id
    AND p.index_id IN (0, 1)
GROUP BY t.name
ORDER BY t.name;
'@ -Transaction $Transaction
}

function Get-IdentityValues {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [System.Data.SqlClient.SqlTransaction]$Transaction = $null
    )

    return Invoke-DatabaseTable -Connection $Connection -Sql @'
SELECT t.name AS TableName,
       CONVERT(decimal(38, 0), ic.seed_value) AS SeedValue,
       CONVERT(decimal(38, 0), ic.increment_value) AS IncrementValue,
       CONVERT(decimal(38, 0), ic.last_value) AS LastValue
FROM sys.tables AS t
JOIN sys.identity_columns AS ic ON ic.object_id = t.object_id
ORDER BY t.name;
'@ -Transaction $Transaction
}

$masterConnection = New-DatabaseConnection -Database 'master'
$sourceConnection = $null
$targetConnection = $null
$cleanupTransaction = $null

try {
    if (-not (Test-Path -LiteralPath $ManifestPath)) {
        throw "MANIFEST_FILE_NOT_FOUND: $ManifestPath"
    }
    if (-not (Test-Path -LiteralPath $BackupDirectory -PathType Container)) {
        throw "BACKUP_DIRECTORY_NOT_FOUND: $BackupDirectory"
    }

    $manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
    $classifications = @('PRESERVE_SECURITY', 'PRESERVE_REFERENCE', 'PRESERVE_CUSTOMERS', 'EMPTY_OPERATIONAL')
    $manifestEntries = @()
    foreach ($classification in $classifications) {
        foreach ($tableName in @($manifest.$classification)) {
            $manifestEntries += [pscustomobject]@{ TableName = [string]$tableName; Classification = $classification }
        }
    }
    $duplicateManifestTables = @($manifestEntries | Group-Object TableName | Where-Object Count -ne 1)
    if ($manifestEntries.Count -ne 142 -or $duplicateManifestTables.Count -gt 0) {
        throw 'MANIFEST_NOT_PROVEN'
    }

    $targetStatus = Invoke-DatabaseTable -Connection $masterConnection -Sql "SELECT state_desc AS StateDescription FROM sys.databases WHERE name = $(ConvertTo-SqlLiteral $TargetDatabase);"
    if ($targetStatus.Rows.Count -ne 1 -or $targetStatus.Rows[0].StateDescription -ne 'ONLINE') {
        throw 'TARGET_DATABASE_NOT_ONLINE'
    }

    $sourceConnection = New-DatabaseConnection -Database $SourceDatabase
    $targetConnection = New-DatabaseConnection -Database $TargetDatabase
    $sourceTables = @((Invoke-DatabaseTable -Connection $sourceConnection -Sql 'SELECT name AS TableName FROM sys.tables ORDER BY name;').Rows | ForEach-Object { [string]$_.TableName })
    $targetTables = @((Invoke-DatabaseTable -Connection $targetConnection -Sql 'SELECT name AS TableName FROM sys.tables ORDER BY name;').Rows | ForEach-Object { [string]$_.TableName })
    $manifestTables = @($manifestEntries | ForEach-Object TableName)
    $missingSourceTables = @($sourceTables | Where-Object { $_ -notin $manifestTables })
    $missingTargetTables = @($targetTables | Where-Object { $_ -notin $manifestTables })
    $staleManifestTables = @($manifestTables | Where-Object { $_ -notin $targetTables })
    if ($sourceTables.Count -ne 142 -or $targetTables.Count -ne 142 -or $missingSourceTables.Count -gt 0 -or $missingTargetTables.Count -gt 0 -or $staleManifestTables.Count -gt 0) {
        throw "MANIFEST_TARGET_MISMATCH SourceMissing=[$($missingSourceTables -join ',')] TargetUnexpected=[$($missingTargetTables -join ',')] Stale=[$($staleManifestTables -join ',')]"
    }

    $sourceProofBefore = Get-CustomerProof -Connection $sourceConnection
    $targetProofBefore = Get-CustomerProof -Connection $targetConnection
    if ($targetProofBefore.Rows[0].CustomerCount -ne 137 -or $targetProofBefore.Rows[0].CustomerSha256 -ne $expectedCustomerHash -or $targetProofBefore.Rows[0].CustomersWithNonZeroSnapshots -ne 15) {
        throw 'PHASE_2_BASELINE_PROOF_FAILED'
    }

    $targetRowsBefore = Get-TableRowCounts -Connection $targetConnection
    $identityBefore = Get-IdentityValues -Connection $targetConnection
    $sourceRowsBefore = Get-TableRowCounts -Connection $sourceConnection
    $sourceFiles = Invoke-DatabaseTable -Connection $masterConnection -Sql "SELECT name AS LogicalName, type_desc AS FileType FROM sys.master_files WHERE database_id = DB_ID($(ConvertTo-SqlLiteral $TargetDatabase)) ORDER BY file_id;"
    $timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $backupFile = Join-Path $BackupDirectory "$TargetDatabase`_PHASE3_$timestamp.bak"
    if (Test-Path -LiteralPath $backupFile) {
        throw 'BACKUP_FILE_ALREADY_EXISTS'
    }

    Write-Output "TARGET_DATABASE=$TargetDatabase"
    Write-Output 'MARS_DISABLED=True'
    Write-Output "MANIFEST_VERIFICATION=PRESERVE_SECURITY:$(@($manifest.PRESERVE_SECURITY).Count);PRESERVE_REFERENCE:$(@($manifest.PRESERVE_REFERENCE).Count);PRESERVE_CUSTOMERS:$(@($manifest.PRESERVE_CUSTOMERS).Count);EMPTY_OPERATIONAL:$(@($manifest.EMPTY_OPERATIONAL).Count);NOT_PROVEN:0"
    Write-Output "BACKUP_PATH=$backupFile"

    if (-not $DiagnosticRollbackOnly) {
        Invoke-DatabaseNonQuery -Connection $masterConnection -TimeoutSeconds 1800 -Sql "BACKUP DATABASE [$TargetDatabase] TO DISK = $(ConvertTo-SqlLiteral $backupFile) WITH COPY_ONLY, CHECKSUM, STATS = 5;"
        Invoke-DatabaseNonQuery -Connection $masterConnection -TimeoutSeconds 600 -Sql "RESTORE VERIFYONLY FROM DISK = $(ConvertTo-SqlLiteral $backupFile) WITH CHECKSUM;"
        $backupHeader = (Invoke-DatabaseTable -Connection $masterConnection -Sql "RESTORE HEADERONLY FROM DISK = $(ConvertTo-SqlLiteral $backupFile);").Rows[0]
        Write-Output "BACKUP_EVIDENCE=Size:$($backupHeader.BackupSize);Start:$($backupHeader.BackupStartDate.ToString('o'));Finish:$($backupHeader.BackupFinishDate.ToString('o'));Type:$($backupHeader.BackupType)"
        Write-Output 'RESTORE_VERIFYONLY=PASSED'
    }
    else {
        Write-Output 'DIAGNOSTIC_MODE=ROLLBACK_ONLY'
    }

    $manifestValues = foreach ($entry in $manifestEntries) {
        "($(ConvertTo-SqlLiteral $entry.TableName), $(ConvertTo-SqlLiteral $entry.Classification))"
    }
    $manifestInsert = $manifestValues -join ",`n    "
    $cleanupSql = @"
IF DB_NAME() <> N'$TargetDatabase'
    THROW 51000, 'TARGET_DATABASE_GUARD_FAILED', 1;

CREATE TABLE #Manifest (TableName sysname NOT NULL PRIMARY KEY, Classification varchar(40) NOT NULL);
INSERT INTO #Manifest (TableName, Classification) VALUES
    $manifestInsert;

IF EXISTS (SELECT name FROM sys.tables EXCEPT SELECT TableName FROM #Manifest)
    THROW 51001, 'MANIFEST_NOT_PROVEN_TARGET_TABLE_MISSING', 1;
IF EXISTS (SELECT TableName FROM #Manifest EXCEPT SELECT name FROM sys.tables)
    THROW 51002, 'MANIFEST_NOT_PROVEN_STALE_TABLE', 1;

DECLARE @TableName sysname, @Sql nvarchar(max);
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
    THROW 51003, 'FOREIGN_KEY_RECHECK_FAILED', 1;
"@

    $cleanupStep = 'BEGIN_TRANSACTION'
    $cleanupTable = 'NONE'
    try {
        Write-Output 'CLEANUP_TRANSACTION=STARTED'
        $cleanupTransaction = $targetConnection.BeginTransaction()
        $cleanupStep = 'CLEANUP_SQL_BATCH'
        $cleanupTable = 'MANIFEST_DRIVEN_TABLES'
        Invoke-DatabaseNonQuery -Connection $targetConnection -TimeoutSeconds 1800 -Sql $cleanupSql -Transaction $cleanupTransaction
        Write-Output 'CLEANUP_TRANSACTION=DATA_APPLIED'
        $cleanupStep = 'DBCC_CHECKCONSTRAINTS'
        $cleanupTable = 'ALL'
        $constraintViolations = Invoke-DatabaseTable -Connection $targetConnection -TimeoutSeconds 600 -Sql 'DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS;' -Transaction $cleanupTransaction
        if ($constraintViolations.Rows.Count -gt 0) {
            throw "CHECKCONSTRAINTS_FAILED: $($constraintViolations.Rows.Count)"
        }
        $cleanupStep = 'COMMIT_GATE'
        $targetProofInTransaction = Get-CustomerProof -Connection $targetConnection -Transaction $cleanupTransaction
        $targetRowsInTransaction = Get-TableRowCounts -Connection $targetConnection -Transaction $cleanupTransaction
        if ($targetProofInTransaction.Rows[0].CustomerCount -ne 137 -or $targetProofInTransaction.Rows[0].CustomerSha256 -ne $expectedCustomerHash -or $targetProofInTransaction.Rows[0].CustomersWithNonZeroSnapshots -ne 0) {
            throw 'CUSTOMER_COMMIT_GATE_FAILED'
        }
        foreach ($entry in $manifestEntries | Where-Object Classification -eq 'EMPTY_OPERATIONAL') {
            $rows = [int64]($targetRowsInTransaction.Rows | Where-Object TableName -eq $entry.TableName | Select-Object -ExpandProperty TotalRows)
            if ($rows -ne 0) {
                $cleanupTable = $entry.TableName
                throw "OPERATIONAL_TABLE_COMMIT_GATE_FAILED: $cleanupTable=$rows"
            }
        }
        $cashProofInTransaction = Invoke-DatabaseTable -Connection $targetConnection -Sql 'SELECT COUNT(*) AS NonZeroCashAccounts FROM dbo.CashAccounts WHERE CurrentBalance <> 0;' -Transaction $cleanupTransaction
        if ($cashProofInTransaction.Rows[0].NonZeroCashAccounts -ne 0) {
            $cleanupTable = 'CashAccounts'
            throw 'CASH_COMMIT_GATE_FAILED'
        }
        if ($DiagnosticRollbackOnly) {
            $cleanupStep = 'DIAGNOSTIC_ROLLBACK'
            $cleanupTransaction.Rollback()
            Write-Output 'CLEANUP_TRANSACTION=DIAGNOSTIC_ROLLED_BACK'
        }
        else {
            $cleanupStep = 'COMMIT'
            $cleanupTransaction.Commit()
            Write-Output 'CLEANUP_TRANSACTION=COMMITTED'
        }
        $cleanupTransaction.Dispose()
        $cleanupTransaction = $null
    }
    catch {
        $cleanupFailure = $_
        try {
            if ($cleanupTransaction) {
                $cleanupTransaction.Rollback()
                $cleanupTransaction.Dispose()
                $cleanupTransaction = $null
                Write-Output 'CLEANUP_TRANSACTION=ROLLED_BACK'
            }
        }
        catch {
            Write-Output ("CLEANUP_ROLLBACK_FAILURE=" + $_.Exception.Message)
        }
        $sqlErrorNumber = if ($cleanupFailure.Exception -is [System.Data.SqlClient.SqlException]) { $cleanupFailure.Exception.Number } else { 'NONE' }
        Write-Output ("CLEANUP_FAILURE_STEP=$cleanupStep;Table=$cleanupTable;SqlErrorNumber=$sqlErrorNumber;Message=" + $cleanupFailure.Exception.Message)
        throw $cleanupFailure
    }

    $targetProofAfter = Get-CustomerProof -Connection $targetConnection
    $targetRowsAfter = Get-TableRowCounts -Connection $targetConnection
    $identityAfter = Get-IdentityValues -Connection $targetConnection
    $sourceProofAfter = Get-CustomerProof -Connection $sourceConnection
    $sourceRowsAfter = Get-TableRowCounts -Connection $sourceConnection

    $expectedSnapshotsAfter = if ($DiagnosticRollbackOnly) { 15 } else { 0 }
    if ($targetProofAfter.Rows[0].CustomerCount -ne 137 -or $targetProofAfter.Rows[0].CustomerSha256 -ne $expectedCustomerHash -or $targetProofAfter.Rows[0].CustomersWithNonZeroSnapshots -ne $expectedSnapshotsAfter) {
        throw 'CUSTOMER_PRESERVATION_OR_SNAPSHOT_RESET_FAILED'
    }
    if ($sourceProofBefore.Rows[0].CustomerCount -ne $sourceProofAfter.Rows[0].CustomerCount -or $sourceProofBefore.Rows[0].CustomerSha256 -ne $sourceProofAfter.Rows[0].CustomerSha256) {
        throw 'SOURCE_CUSTOMER_PROOF_CHANGED'
    }

    if ($DiagnosticRollbackOnly) {
        $restorationChanges = @()
        foreach ($tableName in $targetTables) {
            $beforeRows = [int64]($targetRowsBefore.Rows | Where-Object TableName -eq $tableName | Select-Object -ExpandProperty TotalRows)
            $afterRows = [int64]($targetRowsAfter.Rows | Where-Object TableName -eq $tableName | Select-Object -ExpandProperty TotalRows)
            if ($beforeRows -ne $afterRows) {
                $restorationChanges += $tableName
            }
        }
        if ($restorationChanges.Count -gt 0) {
            throw "DIAGNOSTIC_BASELINE_RESTORATION_FAILED: $($restorationChanges -join ',')"
        }
        Write-Output 'DIAGNOSTIC_BASELINE_RESTORATION=PASSED'
        Write-Output "DIAGNOSTIC_SNAPSHOT_RESTORATION=CustomersWithNonZeroSnapshots:$($targetProofAfter.Rows[0].CustomersWithNonZeroSnapshots)"
        return
    }

    foreach ($entry in $manifestEntries | Where-Object Classification -eq 'EMPTY_OPERATIONAL') {
        $beforeRows = [int64]($targetRowsBefore.Rows | Where-Object TableName -eq $entry.TableName | Select-Object -ExpandProperty TotalRows)
        $afterRows = [int64]($targetRowsAfter.Rows | Where-Object TableName -eq $entry.TableName | Select-Object -ExpandProperty TotalRows)
        if ($afterRows -ne 0) {
            throw "OPERATIONAL_TABLE_NOT_EMPTY: $($entry.TableName)=$afterRows"
        }
        Write-Output "EMPTIED_TABLE=$($entry.TableName);DeletedRows=$beforeRows;TargetRows=$afterRows"
    }

    $sourceChanges = @()
    foreach ($tableName in $sourceTables) {
        $beforeRows = [int64]($sourceRowsBefore.Rows | Where-Object TableName -eq $tableName | Select-Object -ExpandProperty TotalRows)
        $afterRows = [int64]($sourceRowsAfter.Rows | Where-Object TableName -eq $tableName | Select-Object -ExpandProperty TotalRows)
        if ($beforeRows -ne $afterRows) {
            $sourceChanges += $tableName
        }
    }
    if ($sourceChanges.Count -gt 0) {
        throw "SOURCE_ROW_COUNTS_CHANGED: $($sourceChanges -join ',')"
    }

    $identityTables = @($manifestEntries | Where-Object Classification -eq 'EMPTY_OPERATIONAL' | ForEach-Object TableName)
    foreach ($identityRow in $identityBefore.Rows | Where-Object { $_.TableName -in $identityTables }) {
        $afterIdentity = $identityAfter.Rows | Where-Object TableName -eq $identityRow.TableName
        Write-Output "IDENTITY_RESEED=$($identityRow.TableName);OldIdentity=$($identityRow.LastValue);NewIdentity=$($afterIdentity.LastValue)"
    }

    $financialZeroTables = @('AccountingEvents', 'FinancialTransactions', 'JournalEntries', 'JournalEntryLines', 'Payments', 'CustomerLedgerEntries', 'CustomerAdvanceApplications', 'CashMovements', 'ReadyMadeSaleCostPostings', 'ImportedReadyMadeInventoryReceipts', 'ImportedReadyMadeSaleCostPostings')
    $inventoryZeroTables = @('InventoryItems', 'InventoryTransactions', 'Fabrics', 'Fabrics_Inventory', 'FabricRolls', 'ReadyMadeInventoryProducts', 'ImportedReadyMadeProducts')
    $productionZeroTables = @('ProductionOrders', 'ProductionBatches', 'Pieces', 'TrackingEvents', 'Production_Tracking', 'Live_Scan', 'PieceWageRecords')
    $loyaltyZeroTables = @('LoyaltyAccounts', 'LoyaltyTransactions', 'LoyaltyRedemptions', 'ReferralAccounts', 'ReferralTransactions', 'ReferralRewards')
    $payrollZeroTables = @('PayrollPeriods', 'PayrollRecords', 'PayrollItems', 'PieceWageRecords', 'Employee_Draws', 'EmployeeDrawSettlements', 'EmployeeAttendances')
    foreach ($tableName in @($financialZeroTables + $inventoryZeroTables + $productionZeroTables + $loyaltyZeroTables + $payrollZeroTables | Select-Object -Unique)) {
        $rows = [int64]($targetRowsAfter.Rows | Where-Object TableName -eq $tableName | Select-Object -ExpandProperty TotalRows)
        if ($rows -ne 0) {
            throw "ZERO_GATE_FAILED: $tableName=$rows"
        }
        Write-Output "ZERO_GATE=$tableName;Rows=$rows"
    }

    $cashProof = Invoke-DatabaseTable -Connection $targetConnection -Sql 'SELECT COUNT(*) AS NonZeroCashAccounts FROM dbo.CashAccounts WHERE CurrentBalance <> 0;'
    if ($cashProof.Rows[0].NonZeroCashAccounts -ne 0) {
        throw 'CASH_ZERO_GATE_FAILED'
    }
    $fkProof = Invoke-DatabaseTable -Connection $targetConnection -Sql "SELECT SUM(CASE WHEN is_disabled = 1 THEN 1 ELSE 0 END) AS DisabledForeignKeys, SUM(CASE WHEN is_not_trusted = 1 THEN 1 ELSE 0 END) AS UntrustedForeignKeys FROM sys.foreign_keys; SELECT SUM(CASE WHEN is_disabled = 1 THEN 1 ELSE 0 END) AS DisabledTriggers FROM sys.triggers WHERE parent_class = 1;"
    $fkProof.Rows[0] | ForEach-Object { Write-Output "FK_VERIFICATION=Disabled:$($_.DisabledForeignKeys);Untrusted:$($_.UntrustedForeignKeys)" }
    Write-Output "TRIGGER_VERIFICATION=Disabled:$((Invoke-DatabaseTable -Connection $targetConnection -Sql 'SELECT SUM(CASE WHEN is_disabled = 1 THEN 1 ELSE 0 END) AS DisabledTriggers FROM sys.triggers WHERE parent_class = 1;').Rows[0].DisabledTriggers)"
    Write-Output "CUSTOMER_SNAPSHOT_RESET=CustomersWithNonZeroSnapshots:$($targetProofAfter.Rows[0].CustomersWithNonZeroSnapshots)"
    Write-Output "CASH_ZERO_VERIFICATION=NonZeroCashAccounts:$($cashProof.Rows[0].NonZeroCashAccounts)"
    Write-Output 'CONSTRAINT_VERIFICATION=DBCC_CHECKCONSTRAINTS_PASSED'
    Write-Output 'ORPHAN_VERIFICATION=PASSED'
    Write-Output 'SOURCE_NON_IMPACT=PASSED'
    Write-Output 'GOLDEN_BASELINE_READY'
}
finally {
    if ($targetConnection) { $targetConnection.Dispose() }
    if ($sourceConnection) { $sourceConnection.Dispose() }
    if ($masterConnection) { $masterConnection.Dispose() }
}