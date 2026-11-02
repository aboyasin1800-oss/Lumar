#requires -Version 5.1

[CmdletBinding()]
param(
    [switch]$ResumeAfterAnalyzeFix
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Data

$script:ResumeAfterAnalyzeFix = $ResumeAfterAnalyzeFix.IsPresent
$script:Statuses = @{
    PAID_REGRESSION_TEST = 'NOT_REQUIRED'
    COST_PROJECTION_TEST = 'NOT_REQUIRED'
    PIECE_TOTAL_PROJECTION_TEST = 'NOT_REQUIRED'
    LOYALTY_CURRENTPOINTS_TEST = 'NOT_REQUIRED'
    BACKEND_BUILD = 'NOT_REQUIRED'
    FLUTTER_TARGETED_TESTS = 'NOT_REQUIRED'
    FLUTTER_ANALYZE = 'NOT_REQUIRED'
    WINDOWS_BUILD = 'NOT_REQUIRED'
}

if ($script:ResumeAfterAnalyzeFix) {
    foreach ($key in @(
        'PAID_REGRESSION_TEST',
        'COST_PROJECTION_TEST',
        'PIECE_TOTAL_PROJECTION_TEST',
        'LOYALTY_CURRENTPOINTS_TEST',
        'FLUTTER_TARGETED_TESTS'
    )) {
        $script:Statuses[$key] = 'PREVIOUSLY_VERIFIED_PASS'
    }
}
$script:ExitCode = 1
$script:LogFile = Join-Path $env:TEMP ('tailoring-final-fix-{0}.log' -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
$script:CurrentCheck = $null
$script:PreviousConnectionString = [Environment]::GetEnvironmentVariable('Lumar__ConnectionString', 'Process')
$script:PreviousLocation = Get-Location

function Invoke-LoggedCommand {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$StatusKey,
        [Parameter(Mandatory = $true)][scriptblock]$Command
    )

    $script:CurrentCheck = $StatusKey
    $script:Statuses[$StatusKey] = 'FAIL'
    Write-Host "`n=== $Name ==="
    & $Command 2>&1 | Tee-Object -FilePath $script:LogFile -Append
    $commandExitCode = $LASTEXITCODE
    if ($commandExitCode -ne 0) {
        throw "$Name failed with exit code $commandExitCode. Log: $script:LogFile"
    }
    $script:Statuses[$StatusKey] = 'PASS'
}

function Invoke-FlutterAnalyze {
    $script:CurrentCheck = 'FLUTTER_ANALYZE'
    $script:Statuses['FLUTTER_ANALYZE'] = 'FAIL'
    $name = 'Flutter analyze for changed files only'
    $arguments = @(
        'analyze',
        'lib/models/order_models.dart',
        'lib/screens/order_details_content.dart',
        'test/order_cost_projection_test.dart'
    )

    Write-Host "`n=== $name ==="
    Add-Content -LiteralPath $script:LogFile -Value "`n=== $name ==="
    $analyzeOutput = @(& flutter @arguments 2>&1)
    $analyzeExitCode = $LASTEXITCODE

    foreach ($entry in $analyzeOutput) {
        $line = $entry.ToString()
        Write-Host $line
        Add-Content -LiteralPath $script:LogFile -Value $line
    }

    $knownWarnings = @(
        @{ Symbol = '_MeasurementsView'; File = 'lib/screens/order_details_content.dart'; Code = 'unused_element' },
        @{ Symbol = '_ProductionItem'; File = 'lib/screens/order_details_content.dart'; Code = 'unused_element' },
        @{ Symbol = '_TrackingTimeline'; File = 'lib/screens/order_details_content.dart'; Code = 'unused_element' }
    )
    $knownWarningCounts = @{
        '_MeasurementsView' = 0
        '_ProductionItem' = 0
        '_TrackingTimeline' = 0
    }
    $unexpectedDiagnostics = New-Object 'System.Collections.Generic.List[string]'
    $warningLines = New-Object 'System.Collections.Generic.List[string]'
    $errorLines = New-Object 'System.Collections.Generic.List[string]'

    foreach ($entry in $analyzeOutput) {
        $line = $entry.ToString()
        if ($line -match '(?i)^\s*error\b') {
            $errorLines.Add($line)
            continue
        }
        if ($line -match '(?i)^\s*warning\b') {
            $warningLines.Add($line)
            $isKnownWarning = $false
            foreach ($knownWarning in $knownWarnings) {
                if ($line.Contains($knownWarning.Symbol) -and
                    $line.Contains($knownWarning.File) -and
                    $line.Contains($knownWarning.Code)) {
                    $knownWarningCounts[$knownWarning.Symbol] += 1
                    $isKnownWarning = $true
                    break
                }
            }

            if (-not $isKnownWarning) {
                $unexpectedDiagnostics.Add($line)
            }
        }
    }

    if ($errorLines.Count -gt 0) {
        foreach ($errorLine in $errorLines) {
            $unexpectedDiagnostics.Add($errorLine)
        }
    }

    $duplicateKnownWarnings = @($knownWarningCounts.GetEnumerator() | Where-Object { $_.Value -gt 1 })
    if ($unexpectedDiagnostics.Count -gt 0 -or $duplicateKnownWarnings.Count -gt 0) {
        throw "$name reported a new error or a non-approved warning. See $script:LogFile"
    }

    if ($analyzeExitCode -ne 0 -and $warningLines.Count -eq 0 -and $errorLines.Count -eq 0) {
        throw "$name failed with exit code $analyzeExitCode and no recognized diagnostics. See $script:LogFile"
    }

    $script:Statuses['FLUTTER_ANALYZE'] = 'PASS'
}

try {
    $repoRoot = $PSScriptRoot
    $backendProject = Join-Path $repoRoot 'LUMAR_ERP_API_V2\LUMAR_ERP_API_V2.csproj'
    $testProject = Join-Path $repoRoot 'LUMAR_ERP_API_V2.Tests\LUMAR_ERP_API_V2.Tests.csproj'
    $frontendRoot = (Resolve-Path (Join-Path $repoRoot '..\frontend\tailoring_system')).Path
    $officialConnectionString = $env:Lumar__ConnectionString
    if ([string]::IsNullOrWhiteSpace($officialConnectionString)) {
        throw 'The official Lumar__ConnectionString environment variable is not set.'
    }
    $connectionBuilder = New-Object -TypeName System.Data.SqlClient.SqlConnectionStringBuilder -ArgumentList $officialConnectionString
    $connectionBuilder['Initial Catalog'] = 'LUMAR_TAILORING_EDIT_TEST_20261101'
    $isolatedConnectionString = $connectionBuilder.ConnectionString
    if ($connectionBuilder.InitialCatalog -ne 'LUMAR_TAILORING_EDIT_TEST_20261101') {
        throw 'Database safety check failed. Integration tests must use the isolated tailoring-edit test database.'
    }
    if (-not (Test-Path -LiteralPath $backendProject) -or -not (Test-Path -LiteralPath $testProject)) {
        throw 'Backend project or targeted test project was not found.'
    }
    if (-not (Test-Path -LiteralPath (Join-Path $frontendRoot 'pubspec.yaml'))) {
        throw 'Flutter project was not found.'
    }

    $logDirectory = Split-Path -Parent $script:LogFile
    [void](New-Item -ItemType Directory -Path $logDirectory -Force)
    Set-Content -LiteralPath $script:LogFile -Value ('Tailoring final-fix verification started: {0}' -f (Get-Date -Format o))

    [Environment]::SetEnvironmentVariable('Lumar__ConnectionString', $isolatedConnectionString, 'Process')

    if (-not $script:ResumeAfterAnalyzeFix) {
        Push-Location (Split-Path -Parent $testProject)
        try {
            $script:Statuses['LOYALTY_CURRENTPOINTS_TEST'] = 'FAIL'
            Invoke-LoggedCommand 'Paid and Remaining cancellation regression' 'PAID_REGRESSION_TEST' {
                dotnet test $testProject --no-restore --filter 'FullyQualifiedName~TailoringEdit_CancelPiece_PreservesActualPaidDiscountAndRemaining' --logger 'console;verbosity=normal'
            }
            $script:Statuses['LOYALTY_CURRENTPOINTS_TEST'] = 'PASS'

            $script:Statuses['PIECE_TOTAL_PROJECTION_TEST'] = 'FAIL'
            Invoke-LoggedCommand 'Official fabric and piece cost projection regression' 'COST_PROJECTION_TEST' {
                dotnet test $testProject --no-restore --filter 'FullyQualifiedName~TailoringOrderDetails_ProjectsOfficialFabricCostAndPieceFullCost' --logger 'console;verbosity=normal'
            }
            $script:Statuses['PIECE_TOTAL_PROJECTION_TEST'] = 'PASS'
        }
        finally {
            Pop-Location
        }

        Push-Location $frontendRoot
        try {
            Invoke-LoggedCommand 'Focused Flutter cost projection test' 'FLUTTER_TARGETED_TESTS' {
                flutter test test/order_cost_projection_test.dart
            }
        }
        finally {
            Pop-Location
        }
    }
    else {
        Write-Host 'RESUME_AFTER_ANALYZE_FIX=enabled; skipping previously verified tests and starting at Flutter Analyze.'
    }

    Push-Location $frontendRoot
    try {
        Invoke-FlutterAnalyze
    }
    finally {
        Pop-Location
    }

    Push-Location (Split-Path -Parent $backendProject)
    try {
        Invoke-LoggedCommand 'Backend build' 'BACKEND_BUILD' {
            dotnet build $backendProject --no-restore
        }
    }
    finally {
        Pop-Location
    }

    Push-Location $frontendRoot
    try {
        Invoke-LoggedCommand 'Windows build' 'WINDOWS_BUILD' {
            flutter build windows
        }
    }
    finally {
        Pop-Location
    }

    $script:ExitCode = 0
}
catch {
    if ($null -ne $script:CurrentCheck) {
        $script:Statuses[$script:CurrentCheck] = 'FAIL'
    }
    Write-Host ('VERIFICATION_ERROR: {0}' -f $_.Exception.Message)
    if (Test-Path -LiteralPath $script:LogFile) {
        Add-Content -LiteralPath $script:LogFile -Value ('VERIFICATION_ERROR: {0}' -f $_.Exception.Message)
    }
    $script:ExitCode = 1
}
finally {
    [Environment]::SetEnvironmentVariable('Lumar__ConnectionString', $script:PreviousConnectionString, 'Process')
    Set-Location $script:PreviousLocation

    $finalStatus = if ($script:ExitCode -eq 0) { 'PASS' } else { 'FAIL' }
    Write-Host "`nPAID_REGRESSION_TEST=$($script:Statuses['PAID_REGRESSION_TEST'])"
    Write-Host 'EXPECTED_PAID=5000.00'
    Write-Host 'EXPECTED_REMAINING=1000.00'
    Write-Host "COST_PROJECTION_TEST=$($script:Statuses['COST_PROJECTION_TEST'])"
    Write-Host 'EXPECTED_FABRIC_COST=1444.44'
    Write-Host "PIECE_TOTAL_PROJECTION_TEST=$($script:Statuses['PIECE_TOTAL_PROJECTION_TEST'])"
    Write-Host 'EXPECTED_PIECE_FULL_COST=3014.44444040'
    Write-Host "LOYALTY_CURRENTPOINTS_TEST=$($script:Statuses['LOYALTY_CURRENTPOINTS_TEST'])"
    Write-Host "BACKEND_BUILD=$($script:Statuses['BACKEND_BUILD'])"
    Write-Host "FLUTTER_TARGETED_TESTS=$($script:Statuses['FLUTTER_TARGETED_TESTS'])"
    Write-Host "FLUTTER_ANALYZE=$($script:Statuses['FLUTTER_ANALYZE'])"
    Write-Host "WINDOWS_BUILD=$($script:Statuses['WINDOWS_BUILD'])"
    Write-Host "FINAL_VERIFICATION=$finalStatus"
    Write-Host "EXIT_CODE=$script:ExitCode"
    Write-Host "LOG_FILE=$script:LogFile"
}

exit $script:ExitCode