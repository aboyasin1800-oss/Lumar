[CmdletBinding()]
param(
    [string]$ServerInstance = 'YASIN-YASIN\SQLEXPRESS',
    [string]$SourceDatabase = 'LUMAR_ERP_TEST',
    [string]$TargetDatabase = 'LUMAR_ERP_CUSTOMERS_ONLY_TEST',
    [string]$BackupDirectory = 'D:\YASIN\backups',
    [string]$ManifestPath = 'D:\YASIN\scripts\CustomersOnlyPhase2Manifest.json'
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
    CONVERT(varchar(64), HASHBYTES('SHA2_256', CONVERT(varbinary(max), @payload)), 2) AS CustomerSha256
FROM dbo.Customers;
'@
}

function Get-SchemaProof {
    param([System.Data.SqlClient.SqlConnection]$Connection)

    return Invoke-DatabaseTable -Connection $Connection -Sql @'
SELECT 'Tables' AS ObjectType, COUNT(*) AS ObjectCount FROM sys.tables
UNION ALL SELECT 'Views', COUNT(*) FROM sys.views
UNION ALL SELECT 'Procedures', COUNT(*) FROM sys.procedures
UNION ALL SELECT 'Functions', COUNT(*) FROM sys.objects WHERE type IN ('FN', 'IF', 'TF', 'FS', 'FT')
UNION ALL SELECT 'ForeignKeys', COUNT(*) FROM sys.foreign_keys
UNION ALL SELECT 'Triggers', COUNT(*) FROM sys.triggers WHERE parent_class = 1;
'@
}

$masterConnection = New-DatabaseConnection -Database 'master'
$sourceConnection = $null
$targetConnection = $null

try {
    if (-not (Test-Path -LiteralPath $ManifestPath)) {
        throw "MANIFEST_FILE_NOT_FOUND: $ManifestPath"
    }
    if (-not (Test-Path -LiteralPath $BackupDirectory -PathType Container)) {
        throw "BACKUP_DIRECTORY_NOT_FOUND: $BackupDirectory"
    }

    $manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
    $manifestEntries = @()
    foreach ($classification in @('PRESERVE_SECURITY', 'PRESERVE_REFERENCE', 'PRESERVE_CUSTOMERS', 'EMPTY_OPERATIONAL')) {
        foreach ($tableName in @($manifest.$classification)) {
            $manifestEntries += [pscustomobject]@{ TableName = [string]$tableName; Classification = $classification }
        }
    }
    $duplicateManifestTables = @($manifestEntries | Group-Object TableName | Where-Object Count -ne 1)
    if ($duplicateManifestTables.Count -gt 0) {
        throw "MANIFEST_DUPLICATE_TABLES: $($duplicateManifestTables.Name -join ',')"
    }

    $targetExists = Invoke-DatabaseTable -Connection $masterConnection -Sql "SELECT DB_ID($(ConvertTo-SqlLiteral $TargetDatabase)) AS TargetDatabaseId;"
    if (-not $targetExists.Rows[0].IsNull('TargetDatabaseId')) {
        throw "TARGET_DATABASE_ALREADY_EXISTS: $TargetDatabase"
    }

    $sourceConnection = New-DatabaseConnection -Database $SourceDatabase
    $sourceTablesData = Invoke-DatabaseTable -Connection $sourceConnection -Sql 'SELECT name AS TableName FROM sys.tables ORDER BY name;'
    $sourceTables = @($sourceTablesData.Rows | ForEach-Object { [string]$_.TableName })
    $manifestTables = @($manifestEntries | ForEach-Object TableName)
    $missingClassifications = @($sourceTables | Where-Object { $_ -notin $manifestTables })
    $staleClassifications = @($manifestTables | Where-Object { $_ -notin $sourceTables })
    if ($missingClassifications.Count -gt 0 -or $staleClassifications.Count -gt 0 -or $sourceTables.Count -ne 142) {
        throw "MANIFEST_NOT_PROVEN Missing=[$($missingClassifications -join ',')] Stale=[$($staleClassifications -join ',')] SourceTableCount=$($sourceTables.Count)"
    }

    $sourceProofBefore = Get-CustomerProof -Connection $sourceConnection
    $sourceSchemaBefore = Get-SchemaProof -Connection $sourceConnection
    $sourceFiles = Invoke-DatabaseTable -Connection $masterConnection -Sql "SELECT name AS LogicalName, type_desc AS FileType FROM sys.master_files WHERE database_id = DB_ID($(ConvertTo-SqlLiteral $SourceDatabase)) ORDER BY file_id;"
    if ($sourceFiles.Rows.Count -ne 2) {
        throw "SOURCE_FILE_LAYOUT_NOT_PROVEN: expected 2 files, found $($sourceFiles.Rows.Count)"
    }

    $dataPath = (Invoke-DatabaseTable -Connection $masterConnection -Sql "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(4000)) AS DataPath;").Rows[0].DataPath
    $logPath = (Invoke-DatabaseTable -Connection $masterConnection -Sql "SELECT CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(4000)) AS LogPath;").Rows[0].LogPath
    $dataLogicalName = ($sourceFiles.Rows | Where-Object FileType -eq 'ROWS').LogicalName
    $logLogicalName = ($sourceFiles.Rows | Where-Object FileType -eq 'LOG').LogicalName
    $timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $backupFile = Join-Path $BackupDirectory "$SourceDatabase`_PHASE2_$timestamp.bak"
    $targetMdf = Join-Path $dataPath "$TargetDatabase.mdf"
    $targetLdf = Join-Path $logPath "$TargetDatabase`_log.ldf"

    if ((Test-Path -LiteralPath $backupFile) -or (Test-Path -LiteralPath $targetMdf) -or (Test-Path -LiteralPath $targetLdf)) {
        throw 'TARGET_OR_BACKUP_FILE_ALREADY_EXISTS'
    }

    Write-Output "FINAL_MANIFEST=PRESERVE_SECURITY:$(@($manifest.PRESERVE_SECURITY).Count);PRESERVE_REFERENCE:$(@($manifest.PRESERVE_REFERENCE).Count);PRESERVE_CUSTOMERS:$(@($manifest.PRESERVE_CUSTOMERS).Count);EMPTY_OPERATIONAL:$(@($manifest.EMPTY_OPERATIONAL).Count);NOT_PROVEN:0"
    Write-Output "BACKUP_PATH=$backupFile"

    Invoke-DatabaseNonQuery -Connection $masterConnection -TimeoutSeconds 1800 -Sql "BACKUP DATABASE [$SourceDatabase] TO DISK = $(ConvertTo-SqlLiteral $backupFile) WITH COPY_ONLY, CHECKSUM, STATS = 5;"
    Invoke-DatabaseNonQuery -Connection $masterConnection -TimeoutSeconds 600 -Sql "RESTORE VERIFYONLY FROM DISK = $(ConvertTo-SqlLiteral $backupFile) WITH CHECKSUM;"
    $backupHeader = (Invoke-DatabaseTable -Connection $masterConnection -Sql "RESTORE HEADERONLY FROM DISK = $(ConvertTo-SqlLiteral $backupFile);").Rows[0]
    Write-Output "BACKUP_EVIDENCE=Size:$($backupHeader.BackupSize);Start:$($backupHeader.BackupStartDate.ToString('o'));Finish:$($backupHeader.BackupFinishDate.ToString('o'));Type:$($backupHeader.BackupType)"
    Write-Output 'RESTORE_VERIFYONLY=PASSED'

    Invoke-DatabaseNonQuery -Connection $masterConnection -TimeoutSeconds 1800 -Sql "RESTORE DATABASE [$TargetDatabase] FROM DISK = $(ConvertTo-SqlLiteral $backupFile) WITH MOVE $(ConvertTo-SqlLiteral $dataLogicalName) TO $(ConvertTo-SqlLiteral $targetMdf), MOVE $(ConvertTo-SqlLiteral $logLogicalName) TO $(ConvertTo-SqlLiteral $targetLdf), RECOVERY, CHECKSUM, STATS = 5;"
    Write-Output 'RESTORE_RESULT=PASSED'

    $targetConnection = New-DatabaseConnection -Database $TargetDatabase
    $targetProof = Get-CustomerProof -Connection $targetConnection
    $targetSchema = Get-SchemaProof -Connection $targetConnection
    $targetTablesData = Invoke-DatabaseTable -Connection $targetConnection -Sql 'SELECT name AS TableName FROM sys.tables ORDER BY name;'
    $targetTables = @($targetTablesData.Rows | ForEach-Object { [string]$_.TableName })
    $missingTargetManifestTables = @($manifestTables | Where-Object { $_ -notin $targetTables })
    $unexpectedTargetTables = @($targetTables | Where-Object { $_ -notin $manifestTables })

    if ($sourceProofBefore.Rows[0].CustomerCount -ne $targetProof.Rows[0].CustomerCount -or $sourceProofBefore.Rows[0].CustomerSha256 -ne $targetProof.Rows[0].CustomerSha256) {
        throw 'CUSTOMER_PROOF_MISMATCH'
    }
    foreach ($sourceMetric in $sourceSchemaBefore.Rows) {
        $targetMetric = $targetSchema.Rows | Where-Object ObjectType -eq $sourceMetric.ObjectType
        if ($null -eq $targetMetric -or $sourceMetric.ObjectCount -ne $targetMetric.ObjectCount) {
            throw "SCHEMA_PROOF_MISMATCH: $($sourceMetric.ObjectType)"
        }
    }
    if ($missingTargetManifestTables.Count -gt 0 -or $unexpectedTargetTables.Count -gt 0) {
        throw "TARGET_MANIFEST_MISMATCH Missing=[$($missingTargetManifestTables -join ',')] Unexpected=[$($unexpectedTargetTables -join ',')]"
    }

    $sourceProofAfter = Get-CustomerProof -Connection $sourceConnection
    if ($sourceProofBefore.Rows[0].CustomerCount -ne $sourceProofAfter.Rows[0].CustomerCount -or $sourceProofBefore.Rows[0].CustomerSha256 -ne $sourceProofAfter.Rows[0].CustomerSha256) {
        throw 'SOURCE_CUSTOMER_PROOF_CHANGED'
    }

    Write-Output "CUSTOMER_COUNT_SOURCE=$($sourceProofBefore.Rows[0].CustomerCount)"
    Write-Output "CUSTOMER_COUNT_TARGET=$($targetProof.Rows[0].CustomerCount)"
    Write-Output "CUSTOMER_SHA256_SOURCE=$($sourceProofBefore.Rows[0].CustomerSha256)"
    Write-Output "CUSTOMER_SHA256_TARGET=$($targetProof.Rows[0].CustomerSha256)"
    $targetSchema.Rows | Sort-Object ObjectType | ForEach-Object { Write-Output ("{0}_COUNT={1}" -f $_.ObjectType.ToUpperInvariant(), $_.ObjectCount) }
    Write-Output 'MANIFEST_MATCH=PASSED'
    Write-Output 'PHASE_2_CLONE_COMPLETED'
}
finally {
    if ($targetConnection) { $targetConnection.Dispose() }
    if ($sourceConnection) { $sourceConnection.Dispose() }
    if ($masterConnection) { $masterConnection.Dispose() }
}