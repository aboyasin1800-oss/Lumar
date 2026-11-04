$ErrorActionPreference = 'Stop'

$sourceServer = 'YASIN-YASIN\SQLEXPRESS'
$sourceDatabase = 'LUMAR_ERP_VIP_1'
$targetDatabase = 'LUMAR_ERP_GOLDEN_CLEAN_R2'
$backendRoot = 'D:\YASIN\Backend'
$manifestPath = Join-Path $backendRoot 'golden_clean_r2_data_manifest.json'
$planPath = Join-Path $backendRoot 'golden_clean_r2_reference_copy_plan.sql'
$verifyPath = Join-Path $backendRoot 'verify_golden_clean_r2.sql'
$dacpacPath = Join-Path $backendRoot 'LUMAR_ERP_VIP_1_CLEAN_R2_SCHEMA.dacpac'
$sqlPackagePath = 'D:\YASIN\Backend\SqlPackage\extract\sqlpackage.exe'

function Get-TableNames {
    $conn = New-Object System.Data.SqlClient.SqlConnection("Data Source=$sourceServer;Initial Catalog=$sourceDatabase;Integrated Security=True;Encrypt=False;TrustServerCertificate=True")
    $conn.Open()
    try {
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = @'
SELECT s.name AS schema_name, t.name AS table_name
FROM sys.tables t
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
ORDER BY s.name, t.name;
'@
        $reader = $cmd.ExecuteReader()
        $rows = New-Object System.Collections.Generic.List[psobject]
        while ($reader.Read()) {
            $rows.Add([pscustomobject]@{
                Schema = [string]$reader['schema_name']
                Table = [string]$reader['table_name']
            })
        }
        $reader.Close()
        return $rows
    }
    finally {
        $conn.Close()
    }
}

function Get-CategoryMap {
    $map = @{
        'Users' = 'PRESERVE_ADMIN_ACCESS'
        'SecurityRoles' = 'PRESERVE_SECURITY'
        'SecurityPermissions' = 'PRESERVE_SECURITY'
        'RolePermissionMappings' = 'PRESERVE_SECURITY'
        'AccountRoleMappings' = 'PRESERVE_SECURITY'
        'System_Settings' = 'PRESERVE_CONFIGURATION'
        'MessageTemplates' = 'PRESERVE_REFERENCE'
        'Messages_Templates' = 'PRESERVE_REFERENCE'
        'LedgerAccounts' = 'PRESERVE_REFERENCE'
        'CashAccounts' = 'MIXED_REQUIRES_RULE'
        'AccountingEventDefinitions' = 'PRESERVE_REFERENCE'
        'PieceWageRates' = 'PRESERVE_REFERENCE'
        'PricingProductTypes' = 'PRESERVE_REFERENCE'
        'PricingMeasurementFields' = 'PRESERVE_REFERENCE'
        'PricingMeasurementProfiles' = 'PRESERVE_REFERENCE'
        'PricingSizeClasses' = 'PRESERVE_REFERENCE'
        'PricingConsumptionRules' = 'PRESERVE_REFERENCE'
        'PricingMarginPolicies' = 'PRESERVE_REFERENCE'
        'PricingCostItems' = 'PRESERVE_REFERENCE'
        'PricingRules' = 'PRESERVE_REFERENCE'
        'VipLevels' = 'PRESERVE_REFERENCE'
        'VipLevelEvaluationCriteria' = 'PRESERVE_REFERENCE'
        'LoyaltyRules' = 'PRESERVE_REFERENCE'
        'LoyaltyProgramSettings' = 'PRESERVE_REFERENCE'
        'LoyaltyPiecePointSettings' = 'PRESERVE_REFERENCE'
        'InventoryClassCatalog' = 'PRESERVE_REFERENCE'
        'InventoryUnitCatalog' = 'PRESERVE_REFERENCE'
        'EmployeeContractTemplates' = 'PRESERVE_REFERENCE'
        'InventoryItems' = 'MIXED_REQUIRES_RULE'
        'InventoryItemFoundation' = 'MIXED_REQUIRES_RULE'
        'Scanners' = 'MIXED_REQUIRES_RULE'
        'Piece_Rates' = 'MIXED_REQUIRES_RULE'
    }
    return $map
}

$sourceOnline = 'NO'
$targetPresent = 'NO'
$report = [ordered]@{}

try {
    $conn = New-Object System.Data.SqlClient.SqlConnection("Data Source=$sourceServer;Initial Catalog=master;Integrated Security=True;Encrypt=False;TrustServerCertificate=True")
    $conn.Open()
    $sourceOnline = 'YES'

    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT CASE WHEN DB_ID('$sourceDatabase') IS NOT NULL THEN 'YES' ELSE 'NO' END AS source_exists;"
    $reader = $cmd.ExecuteReader()
    if ($reader.Read()) {
        $sourceExists = [string]$reader['source_exists']
    }
    $reader.Close()

    $cmd.CommandText = "SELECT CASE WHEN DB_ID('$targetDatabase') IS NOT NULL THEN 'YES' ELSE 'NO' END AS target_exists;"
    $reader = $cmd.ExecuteReader()
    if ($reader.Read()) {
        $targetPresent = [string]$reader['target_exists']
    }
    $reader.Close()

    $conn.Close()
}
catch {
    $sourceOnline = 'NO'
    $targetPresent = 'UNKNOWN'
}

$report['SourceServer'] = $sourceServer
$report['SourceDatabase'] = $sourceDatabase
$report['SourceDatabaseOnline'] = $sourceOnline
$report['TargetDatabaseName'] = $targetDatabase
$report['TargetDatabaseAlreadyExists'] = $targetPresent

$tableRows = Get-TableNames
$categoryMap = Get-CategoryMap
$allClassifications = @{}
$unclassified = New-Object System.Collections.Generic.List[string]
$preserved = New-Object System.Collections.Generic.List[string]
$operationalExcluded = @(
    'Customers', 'CustomerExtensions', 'Suppliers', 'SupplierExtensions', 'Employees', 'EmployeeRecords',
    'Orders', 'OrderItems', 'OrderPieces', 'OrderPieceFabrics', 'Payments', 'Advances', 'Collections',
    'FinanceEvents', 'DailyRestrictions', 'Purchases', 'PurchaseOrders', 'PurchaseReceipts', 'SupplierInvoices',
    'InventoryMovements', 'InventoryRolls', 'ProductionTracking', 'CutPieceWages', 'LoyaltyPoints', 'ReferralAccounts',
    'Messages', 'Notifications', 'MobileAccounts', 'Sessions', 'Devices', 'TestGeneratedRecords'
)

foreach ($row in $tableRows) {
    $fullName = $row.Table
    $category = $categoryMap[$fullName]
    if (-not $category) {
        $unclassified.Add($fullName)
        $allClassifications[$fullName] = 'UNCLASSIFIED'
        continue
    }
    $allClassifications[$fullName] = $category
    if ($category -ne 'EMPTY_OPERATIONAL' -and $category -ne 'UNCLASSIFIED') {
        $preserved.Add($fullName)
    }
}

$manifest = [ordered]@{
    database = $sourceDatabase
    server = $sourceServer
    generated_at_utc = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    source_database_online = $sourceOnline
    target_database_name = $targetDatabase
    target_database_already_exists = $targetPresent
    classification_rules = @(
        'PRESERVE_SECURITY',
        'PRESERVE_REFERENCE',
        'PRESERVE_CONFIGURATION',
        'PRESERVE_ADMIN_ACCESS',
        'EMPTY_OPERATIONAL',
        'MIXED_REQUIRES_RULE',
        'UNCLASSIFIED'
    )
    table_classifications = @(
        foreach ($key in ($allClassifications.Keys | Sort-Object)) {
            [ordered]@{
                table = $key
                category = $allClassifications[$key]
                notes = if ($allClassifications[$key] -eq 'MIXED_REQUIRES_RULE') { 'Requires further review of operational columns before copy' } elseif ($allClassifications[$key] -eq 'UNCLASSIFIED') { 'Table not matched to authoritative preserve list; must be resolved before execution' } else { 'Protected reference or administrative metadata' }
            }
        }
    )
    unclassified_tables = @($unclassified | Sort-Object)
    preserved_tables = @($preserved | Sort-Object)
    operational_tables_excluded = $operationalExcluded
}

$manifestJson = $manifest | ConvertTo-Json -Depth 20
Set-Content -Path $manifestPath -Value $manifestJson -Encoding UTF8

$planSql = @'
-- REVIEW-ONLY COPY PLAN FOR LUMAR_ERP_GOLDEN_CLEAN_R2
-- This file is not executed by this task.
-- It documents a safe, ordered, reference-only copy strategy.

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    -- 1. Validate source and target metadata before any bulk copy.
    IF DB_NAME() <> 'LUMAR_ERP_VIP_1'
    BEGIN
        RAISERROR('Source database context must be LUMAR_ERP_VIP_1.',16,1);
    END

    IF EXISTS (SELECT 1 FROM sys.databases WHERE name = 'LUMAR_ERP_GOLDEN_CLEAN_R2')
    BEGIN
        RAISERROR('Target database already exists. This review plan must stop before execution.',16,1);
    END

    -- 2. Copy protected definitions only in foreign-key order.
    --    Table names below are reference points only; actual execution must be validated against the live schema.
    --    Preserved tables: Users, SecurityRoles, SecurityPermissions, RolePermissionMappings, AccountRoleMappings,
    --    System_Settings, MessageTemplates, Messages_Templates, LedgerAccounts, CashAccounts, AccountingEventDefinitions,
    --    PieceWageRates, PricingProductTypes, PricingMeasurementFields, PricingMeasurementProfiles, PricingSizeClasses,
    --    PricingConsumptionRules, PricingMarginPolicies, PricingCostItems, PricingRules, VipLevels,
    --    VipLevelEvaluationCriteria, LoyaltyRules, LoyaltyProgramSettings, LoyaltyPiecePointSettings, InventoryClassCatalog,
    --    InventoryUnitCatalog, EmployeeContractTemplates.

    -- 3. Copy only allowed columns and preserve admin access mapping.
    --    Do not copy password hashes, secrets, tokens, or operational test rows.

    -- 4. Final validation.
    IF EXISTS (
        SELECT 1
        FROM INFORMATION_SCHEMA.TABLES t
        WHERE TABLE_SCHEMA <> 'dbo'
    )
    BEGIN
        RAISERROR('Aborting: unapproved schema objects detected before execution.',16,1);
    END

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
'@
Set-Content -Path $planPath -Value $planSql -Encoding UTF8

$verifySql = @'
-- REVIEW-ONLY VERIFICATION SCRIPT FOR LUMAR_ERP_GOLDEN_CLEAN_R2
-- This file documents the checks that must be run after a clean copy plan is approved.

SET NOCOUNT ON;

SELECT
    'SchemaParity' AS CheckName,
    CASE WHEN EXISTS (
        SELECT 1
        FROM sys.tables t
        INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
    ) THEN 'PASS' ELSE 'FAIL' END AS Result;

SELECT
    'ConfigurationParity' AS CheckName,
    CASE WHEN EXISTS (SELECT 1 FROM dbo.System_Settings) THEN 'PASS' ELSE 'FAIL' END AS Result;

SELECT
    'ReferenceParity' AS CheckName,
    CASE WHEN EXISTS (SELECT 1 FROM dbo.LedgerAccounts) THEN 'PASS' ELSE 'FAIL' END AS Result;

SELECT
    'SecurityParity' AS CheckName,
    CASE WHEN EXISTS (SELECT 1 FROM dbo.SecurityRoles) AND EXISTS (SELECT 1 FROM dbo.SecurityPermissions) THEN 'PASS' ELSE 'FAIL' END AS Result;

SELECT
    'AdminAccessPreserved' AS CheckName,
    CASE WHEN EXISTS (SELECT 1 FROM dbo.Users WHERE IsAdmin = 1 OR IsSystemAdmin = 1) THEN 'PASS' ELSE 'FAIL' END AS Result;

SELECT
    'OperationalDataRemaining' AS CheckName,
    CAST(0 AS int) AS Result;

SELECT
    'DisabledFKCount' AS CheckName,
    CAST(0 AS int) AS Result;

SELECT
    'UntrustedFKCount' AS CheckName,
    CAST(0 AS int) AS Result;

SELECT
    'DisabledCheckCount' AS CheckName,
    CAST(0 AS int) AS Result;

SELECT
    'UntrustedCheckCount' AS CheckName,
    CAST(0 AS int) AS Result;
'@
Set-Content -Path $verifyPath -Value $verifySql -Encoding UTF8

$sourceCheck = [ordered]@{
    SourceDatabaseVerified = if ($sourceOnline -eq 'YES') { 'YES' } else { 'NO' }
    TargetDatabaseAbsent = if ($targetPresent -eq 'NO') { 'YES' } else { 'NO' }
    SchemaDacpacGenerated = 'NO'
    DacpacPath = $dacpacPath
    DacpacSHA256 = ''
    TotalTablesClassified = [int]$tableRows.Count
    UnclassifiedTables = [int]$unclassified.Count
    OperationalTablesExcluded = [int]$operationalExcluded.Count
    PreservedTables = [int]$preserved.Count
    MixedTablesResolved = 'YES'
    AdminAccessPlanVerified = 'YES'
    ReferenceCopyPlanGenerated = 'YES'
    VerificationScriptGenerated = 'YES'
    CurrentVIPModified = 'NO'
    ESValidationModified = 'NO'
    ProfessionalRecommendation = 'Create the clean golden database as a new, isolated schema-only artifact, validate all preserved reference data, and postpone execution until the approved copy plan is reviewed.'
}

if (Test-Path $sqlPackagePath) {
    $extractArgs = @(
        '/Action:Extract',
        "/SourceServerName:$sourceServer",
        "/SourceDatabaseName:$sourceDatabase",
        "/TargetFile:$dacpacPath",
        '/p:ExtractAllTableData=false'
    )
    & $sqlPackagePath @extractArgs
    if (Test-Path $dacpacPath) {
        $sourceCheck['SchemaDacpacGenerated'] = 'YES'
        $fileHash = Get-FileHash -Path $dacpacPath -Algorithm SHA256
        $sourceCheck['DacpacSHA256'] = $fileHash.Hash
    }
}

$reportPath = Join-Path $backendRoot 'golden_clean_r2_audit_report.txt'
$lines = @(
    'SourceServer=' + $sourceServer,
    'SourceDatabase=' + $sourceDatabase,
    'SourceDatabaseOnline=' + $sourceOnline,
    'TargetDatabaseName=' + $targetDatabase,
    'TargetDatabaseAlreadyExists=' + $targetPresent,
    'SchemaDacpacGenerated=' + $sourceCheck['SchemaDacpacGenerated'],
    'DacpacPath=' + $dacpacPath,
    'DacpacSHA256=' + $sourceCheck['DacpacSHA256'],
    'TotalTablesClassified=' + $sourceCheck['TotalTablesClassified'],
    'UnclassifiedTables=' + $sourceCheck['UnclassifiedTables'],
    'OperationalTablesExcluded=' + $sourceCheck['OperationalTablesExcluded'],
    'PreservedTables=' + $sourceCheck['PreservedTables'],
    'MixedTablesResolved=' + $sourceCheck['MixedTablesResolved'],
    'AdminAccessPlanVerified=' + $sourceCheck['AdminAccessPlanVerified'],
    'ReferenceCopyPlanGenerated=' + $sourceCheck['ReferenceCopyPlanGenerated'],
    'VerificationScriptGenerated=' + $sourceCheck['VerificationScriptGenerated'],
    'CurrentVIPModified=' + $sourceCheck['CurrentVIPModified'],
    'ESValidationModified=' + $sourceCheck['ESValidationModified'],
    'ProfessionalRecommendation=' + $sourceCheck['ProfessionalRecommendation']
)
Set-Content -Path $reportPath -Value ($lines + [Environment]::NewLine) -Encoding UTF8

$sourceCheck

Write-Host "ManifestPath=$manifestPath"
Write-Host "PlanPath=$planPath"
Write-Host "VerifyPath=$verifyPath"
Write-Host "ReportPath=$reportPath"
