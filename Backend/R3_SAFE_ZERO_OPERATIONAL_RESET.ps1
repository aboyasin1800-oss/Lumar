param(
    [string]$Server = 'YASIN-YASIN\SQLEXPRESS',
    [string]$Database = 'LUMAR_ERP_ES_VALIDATION_R3',
    [string]$ManifestPath = '',
    [string]$DependencyPath = '',
    [ValidateSet('Preview','Execute','Verify')]
    [string]$Mode = 'Preview',
    [switch]$AllowExecute,
    [switch]$AllowTestClone
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $PSScriptRoot 'R3_RESET_REFERENCE_MANIFEST.csv'
}

if ([string]::IsNullOrWhiteSpace($DependencyPath)) {
    $DependencyPath = Join-Path $PSScriptRoot 'R3_RESET_DEPENDENCY_GRAPH.csv'
}

$approvedDb = 'LUMAR_ERP_ES_VALIDATION_R3'
$testCloneDb = 'LUMAR_ERP_ES_VALIDATION_R3_RESET_PROOF'

function Get-ManifestData {
    param([string]$Path)
    if (-not (Test-Path $Path)) {
        throw "Manifest file not found: $Path"
    }
    $rows = Import-Csv -Path $Path -Delimiter ';'
    if (-not $rows) {
        throw "Manifest file is empty: $Path"
    }
    return $rows
}

function Invoke-SqlQuery {
    param(
        [string]$Query,
        [string]$Server,
        [string]$Database
    )

    $args = @(
        '-S', $Server,
        '-d', $Database,
        '-E',
        '-I',
        '-h', '-1',
        '-W',
        '-Q', $Query
    )

    $output = & sqlcmd @args 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "sqlcmd failed: $($output | Out-String)"
    }
    return $output
}

function Get-CurrentTableCount {
    param(
        [string]$Server,
        [string]$Database,
        [string]$Schema,
        [string]$Table
    )

    $query = "SET NOCOUNT ON; SELECT COUNT_BIG(*) FROM [$Schema].[$Table];"
    $output = Invoke-SqlQuery -Query $query -Server $Server -Database $Database
    $value = ($output | Select-String -Pattern '\d+' -AllMatches | ForEach-Object { $_.Matches.Value } | Select-Object -Last 1)
    if ($null -eq $value) { return 0 }
    return [long]$value
}

function Get-TableSqlName {
    param([pscustomobject]$Row)
    $schema = if ($null -ne $Row.schema_name) { $Row.schema_name } elseif ($null -ne $Row.schemaName) { $Row.schemaName } else { 'dbo' }
    $table = if ($null -ne $Row.table_name) { $Row.table_name } elseif ($null -ne $Row.tableName) { $Row.tableName } elseif ($null -ne $Row.table) { $Row.table } else { '' }
    return @($schema.Trim(), $table.Trim())
}

function Assert-AuthorizedTarget {
    param([string]$TargetDb,[switch]$AllowTestClone)
    $isExactLive = $TargetDb -eq $approvedDb
    $isApprovedClone = $AllowTestClone -and $TargetDb -eq $testCloneDb
    if (-not ($isExactLive -or $isApprovedClone)) {
        throw "Unauthorized database target: $TargetDb. Only $approvedDb is permitted for the live reset; an isolated clone may be used only when -AllowTestClone is set and the database name matches $testCloneDb."
    }
}

function Assert-ManifestConsistency {
    param(
        [System.Collections.IEnumerable]$ManifestRows,
        [System.Collections.IEnumerable]$DependencyRows
    )

    $tableNames = @($ManifestRows | ForEach-Object { $_.table_name })
    if ($tableNames.Count -ne 179) {
        throw "Manifest table count mismatch. Expected 179 user tables, found $($tableNames.Count)."
    }

    $dups = $tableNames | Group-Object | Where-Object { $_.Count -gt 1 }
    if ($dups) {
        throw "Duplicate table names in manifest: $($dups.Name -join ', ')"
    }

    $fkCount = @($DependencyRows).Count
    if ($fkCount -ne 305) {
        throw "Dependency graph count mismatch. Expected 305 FKs, found $fkCount."
    }

    $pure = $ManifestRows | Where-Object { $_.current_state_role -eq 'PURE_OPERATIONAL' }
    $preserve = $ManifestRows | Where-Object { $_.current_state_role -eq 'PRESERVE_BASELINE' }
    $mixed = $ManifestRows | Where-Object { $_.current_state_role -eq 'MIXED_RULE' }
    $exceptions = $ManifestRows | Where-Object { $_.current_state_role -eq 'EXCEPTION_REVIEW' }

    if ($pure.Count -lt 1) { throw 'No PURE_OPERATIONAL tables were found in the manifest.' }
    if ($preserve.Count -lt 1) { throw 'No PRESERVE_BASELINE tables were found in the manifest.' }
    if ($mixed.Count -lt 1) { throw 'No MIXED_RULE tables were found in the manifest.' }
    if ($exceptions.Count -lt 1) { throw 'No EXCEPTION_REVIEW tables were found in the manifest.' }
}

function Build-DeletePlan {
    param(
        [System.Collections.IEnumerable]$ManifestRows,
        [System.Collections.IEnumerable]$DependencyRows
    )

    $candidateStates = @('PURE_OPERATIONAL', 'MIXED_RULE', 'EXCEPTION_REVIEW')
    $protectedStates = @('PRESERVE_BASELINE')

    $candidateRows = @($ManifestRows | Where-Object { $_.current_state_role -in $candidateStates })
    $protectedRows = @($ManifestRows | Where-Object { $_.current_state_role -in $protectedStates })
    $candidateTables = @($candidateRows | ForEach-Object { $_.table_name } | Select-Object -Unique | Sort-Object)
    $protectedTables = @($protectedRows | ForEach-Object { $_.table_name } | Select-Object -Unique)

    if (-not $candidateTables -or $candidateTables.Count -eq 0) {
        throw 'No operational reset candidates were found in the manifest.'
    }

    $effectiveTables = @($candidateTables | Where-Object { $_ -notin $protectedTables })
    if (-not $effectiveTables -or $effectiveTables.Count -eq 0) {
        throw 'No manifest entries remain after excluding protected baseline tables.'
    }

    $deletionOrder = @()
    foreach ($table in $effectiveTables) {
        $dependencyCount = @(
            $DependencyRows |
            Where-Object { $_.child_table -eq $table -and $_.parent_table -in $effectiveTables }
        ).Count

        $deletionOrder += [pscustomobject]@{
            TableName = $table
            DependencyWeight = $dependencyCount
        }
    }

    $ordered = @(
        $deletionOrder |
        Sort-Object -Property @(
            @{ Expression = 'DependencyWeight'; Descending = $true },
            @{ Expression = 'TableName'; Descending = $false }
        )
    )
    return @($ordered | ForEach-Object { [pscustomobject]@{ TableName = $_.TableName } })
}

function Build-ExecutionSql {
    param(
        [System.Collections.IEnumerable]$DeletePlan
    )

    if (-not $DeletePlan -or $DeletePlan.Count -eq 0) {
        throw 'No operational delete targets were found for the approved reset plan.'
    }

    $sqlStatements = @()
    foreach ($item in $DeletePlan) {
        $sqlStatements += "DELETE FROM [dbo].[$($item.TableName)];"
    }

    return $sqlStatements
}

Assert-AuthorizedTarget -TargetDb $Database -AllowTestClone:$AllowTestClone
$manifestRows = Get-ManifestData -Path $ManifestPath
$dependencyRows = Get-ManifestData -Path $DependencyPath
Assert-ManifestConsistency -ManifestRows $manifestRows -DependencyRows $dependencyRows

$pureRows = @($manifestRows | Where-Object { $_.current_state_role -eq 'PURE_OPERATIONAL' })
$preserveRows = @($manifestRows | Where-Object { $_.current_state_role -eq 'PRESERVE_BASELINE' })
$mixedRows = @($manifestRows | Where-Object { $_.current_state_role -eq 'MIXED_RULE' })
$exceptionRows = @($manifestRows | Where-Object { $_.current_state_role -eq 'EXCEPTION_REVIEW' })
$operationalRows = @(
    $manifestRows |
    Where-Object { $_.current_state_role -in @('PURE_OPERATIONAL', 'MIXED_RULE', 'EXCEPTION_REVIEW') } |
    Where-Object { $_.table_name -notin $preserveRows.table_name }
)

$baselineCheck = @(
    'System_Settings','CashAccounts','LedgerAccounts','AccountingEventDefinitions','AccountRoleMappings',
    'InventoryClassCatalog','InventoryUnitCatalog','PricingProductTypes','MessageTemplates','Messages_Templates',
    'SecurityRoles','SecurityPermissions','RolePermissionMappings','UserRoleAssignments','UserClaimMappings','Users'
)

if ($Mode -eq 'Preview') {
    Write-Host 'R3 SAFE ZERO-OPERATIONAL RESET PREVIEW'
    Write-Host "Target database: $Database"
    Write-Host "Pure operational tables: $($pureRows.Count)"
    Write-Host "Preserve baseline tables: $($preserveRows.Count)"
    Write-Host "Mixed-rule tables: $($mixedRows.Count)"
    Write-Host "Exception tables: $($exceptionRows.Count)"
    Write-Host "Operational cleanup candidates: $($operationalRows.Count)"
    Write-Host ''

    foreach ($row in $operationalRows) {
        $schema = 'dbo'
        $table = $row.table_name
        $current = Get-CurrentTableCount -Server $Server -Database $Database -Schema $schema -Table $table
        Write-Host "[$schema].[$table] -> current rows = $current ; planned action = DELETE ALL rows"
    }

    foreach ($table in $baselineCheck) {
        $current = Get-CurrentTableCount -Server $Server -Database $Database -Schema 'dbo' -Table $table
        Write-Host "Protected baseline check [$table] -> current rows = $current"
    }

    Write-Host ''
    Write-Host 'Preview guard status: PASS'
    Write-Host 'No DELETE has been executed. This is a read-only preview.'
    exit 0
}

if ($Mode -eq 'Verify') {
    Write-Host 'R3 SAFE ZERO-OPERATIONAL RESET VERIFY (read-only)'
    Write-Host "Target database: $Database"

    foreach ($row in $pureRows) {
        $schema = 'dbo'
        $table = $row.table_name
        $count = Get-CurrentTableCount -Server $Server -Database $Database -Schema $schema -Table $table
        Write-Host "[$schema].[$table] => $count rows"
    }

    foreach ($table in $baselineCheck) {
        $count = Get-CurrentTableCount -Server $Server -Database $Database -Schema 'dbo' -Table $table
        Write-Host "Preserve baseline [$table] => $count rows"
    }

    Write-Host 'Verify guard status: PASS (read-only validation only)'
    exit 0
}

if ($Mode -eq 'Execute') {
    if (-not $AllowExecute) {
        throw 'Execute mode requires -AllowExecute. No destructive reset was executed.'
    }

    $deletePlan = Build-DeletePlan -ManifestRows $manifestRows -DependencyRows $dependencyRows
    $deleteSql = Build-ExecutionSql -DeletePlan $deletePlan

    Write-Host 'Executing safe reset plan for PURE_OPERATIONAL tables only...'
    Write-Host "Delete targets: $($deletePlan.Count)"
    Write-Host "Protected mixed-rule tables excluded: $($mixedRows.Count)"
    Write-Host "Exception tables excluded: $($exceptionRows.Count)"

    $disableConstraints = @()
    foreach ($item in $deletePlan) {
        $disableConstraints += "ALTER TABLE [dbo].[$($item.TableName)] NOCHECK CONSTRAINT ALL;"
    }

    $reEnableConstraints = @()
    foreach ($item in $deletePlan) {
        $reEnableConstraints += "ALTER TABLE [dbo].[$($item.TableName)] WITH CHECK CHECK CONSTRAINT ALL;"
    }

    $transactionSql = "SET QUOTED_IDENTIFIER ON; SET ANSI_WARNINGS ON; SET XACT_ABORT ON; BEGIN TRY; BEGIN TRANSACTION; " + ($disableConstraints -join ' ') + ($deleteSql -join ' ') + ($reEnableConstraints -join ' ') + " COMMIT TRANSACTION; END TRY BEGIN CATCH; IF @@TRANCOUNT > 0 ROLLBACK; DECLARE @msg nvarchar(max)=ERROR_MESSAGE(); RAISERROR(@msg,16,1); END CATCH;"

    $executionOutput = Invoke-SqlQuery -Query $transactionSql -Server $Server -Database $Database
    Write-Host 'Real execution completed successfully.'
    $executionOutput | ForEach-Object { Write-Host $_ }
    exit 0
}

throw "Unknown mode: $Mode"
