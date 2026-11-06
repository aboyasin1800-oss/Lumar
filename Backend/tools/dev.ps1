[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('doctor','restore','build','test','flutter','run','stop','smoke','db','db-verify','verify','verify-full','map','json','help')]
    [string]$Action = 'doctor',

    [switch]$Force,
    [switch]$Full,
    [switch]$NoBuild,
    [string]$Filter = 'MessageWriteServiceTests',
    [int]$Port = 5093,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$RemainingArgs
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Script:ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$Script:BackendRoot = Join-Path $Script:ProjectRoot 'LUMAR_ERP_API_V2'
$Script:BackendProject = Join-Path $Script:BackendRoot 'LUMAR_ERP_API_V2.csproj'
$Script:TestsProject = Join-Path $Script:ProjectRoot 'LUMAR_ERP_API_V2.Tests\LUMAR_ERP_API_V2.Tests.csproj'
$Script:SolutionPath = Join-Path $Script:BackendRoot 'LUMAR_ERP_API_V2.slnx'
$Script:VerificationRoot = Join-Path $Script:ProjectRoot '.verification'
$Script:ToolLogRoot = Join-Path $Script:VerificationRoot 'tooling'
$Script:DefaultServer = 'YASIN-YASIN\SQLEXPRESS'
$Script:DefaultDatabase = 'LUMAR_ERP_ES_VALIDATION_R2'
$Script:RuntimePidPath = Join-Path $Script:VerificationRoot 'backend_5093.pid'
$Script:RuntimeOutPath = Join-Path $Script:VerificationRoot 'backend_5093_out.txt'
$Script:RuntimeErrPath = Join-Path $Script:VerificationRoot 'backend_5093_err.txt'
$Script:FlutterRoot = 'D:\YASIN\frontend\tailoring_system'

if ($RemainingArgs) {
    foreach ($arg in $RemainingArgs) {
        if ($arg -ieq 'full') { $script:Full = $true }
        if ($arg -ieq 'db') { $script:Action = 'db-verify' }
    }
}

function Ensure-ToolDirectories {
    New-Item -ItemType Directory -Force -Path $Script:ToolLogRoot | Out-Null
    New-Item -ItemType Directory -Force -Path $Script:VerificationRoot | Out-Null
}

function Write-StageResult {
    param(
        [string]$Name,
        [string]$Status,
        [string]$Detail
    )

    $prefix = if ($Status -eq 'PASS') { 'PASS' } else { 'FAIL' }
    Write-Host "[$prefix] $Name - $Detail"
}

function Get-ExpectedDbGuard {
    $actual = $Script:DefaultDatabase
    if (-not $actual) {
        throw 'Database guard was not resolved.'
    }
    return $actual
}

function Test-SqlDatabaseIdentity {
    param(
        [string]$Database = $Script:DefaultDatabase,
        [string]$Server = $Script:DefaultServer
    )

    $query = "SELECT name FROM sys.databases WHERE name = '$Database';"
    $output = & sqlcmd -S $Server -d master -E -Q $query 2>$null
    if ($LASTEXITCODE -ne 0) { return $false }

    foreach ($row in $output) {
        $trimmed = [string]$row
        if (-not [string]::IsNullOrWhiteSpace($trimmed)) {
            $trimmed = $trimmed.Trim()
            if ($trimmed -eq $Database) { return $true }
        }
    }

    return $false
}

function Resolve-ProjectState {
    $issues = @()
    if (-not (Test-Path $Script:BackendProject)) { $issues += "Backend project missing: $Script:BackendProject" }
    if (-not (Test-Path $Script:TestsProject)) { $issues += "Test project missing: $Script:TestsProject" }
    if (-not (Test-Path $Script:SolutionPath)) { $issues += "Solution missing: $Script:SolutionPath" }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { $issues += 'dotnet SDK unavailable.' }
    if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) { $issues += 'sqlcmd not available.' }
    if (-not (Get-Command flutter -ErrorAction SilentlyContinue)) { $issues += 'flutter SDK not available.' }
    return $issues
}

function Invoke-Doctor {
    Ensure-ToolDirectories
    $issues = Resolve-ProjectState
    $records = @()

    $dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
    $dotnetStatus = if ($dotnetCmd) { 'PASS' } else { 'FAIL' }
    $dotnetDetail = if ($dotnetCmd) { (& dotnet --version 2>$null | Select-Object -First 1) } else { 'dotnet not found.' }
    $records += [PSCustomObject]@{ Name = 'Dotnet SDK'; Status = $dotnetStatus; Detail = $dotnetDetail }

    $flutterCmd = Get-Command flutter -ErrorAction SilentlyContinue
    $flutterStatus = if ($flutterCmd) { 'PASS' } else { 'FAIL' }
    $flutterDetail = if ($flutterCmd) { (& flutter --version 2>$null | Select-Object -First 1) } else { 'flutter not found.' }
    $records += [PSCustomObject]@{ Name = 'Flutter SDK'; Status = $flutterStatus; Detail = $flutterDetail }

    $records += [PSCustomObject]@{ Name = 'PowerShell'; Status = 'PASS'; Detail = $PSVersionTable.PSVersion.ToString() }
    $records += [PSCustomObject]@{ Name = 'Solution'; Status = if (Test-Path $Script:SolutionPath) { 'PASS' } else { 'FAIL' }; Detail = $Script:SolutionPath }
    $records += [PSCustomObject]@{ Name = 'Backend project'; Status = if (Test-Path $Script:BackendProject) { 'PASS' } else { 'FAIL' }; Detail = $Script:BackendProject }
    $records += [PSCustomObject]@{ Name = 'Test project'; Status = if (Test-Path $Script:TestsProject) { 'PASS' } else { 'FAIL' }; Detail = $Script:TestsProject }
    $dbStatus = if (Test-SqlDatabaseIdentity -Database $Script:DefaultDatabase -Server $Script:DefaultServer) { 'PASS' } else { 'FAIL' }
    $records += [PSCustomObject]@{ Name = 'SQL server'; Status = $dbStatus; Detail = "Expected DB: $($Script:DefaultDatabase) on $($Script:DefaultServer)" }
    $records += [PSCustomObject]@{ Name = 'Preferred backend port'; Status = 'PASS'; Detail = '127.0.0.1:5093' }

    foreach ($record in $records) {
        Write-StageResult -Name $record.Name -Status $record.Status -Detail $record.Detail
    }

    $issueCount = @($issues).Count
    if ($issueCount -gt 0) {
        Write-Host "DOCTOR_FAIL_COUNT=$issueCount"
        foreach ($issue in $issues) { Write-Host "DOCTOR_FAILURE=$issue" }
        exit 1
    }

    Write-Host 'DOCTOR_STATUS=PASS'
    return 0
}

function Invoke-Restore {
    Ensure-ToolDirectories
    $assets = @(
        (Join-Path $Script:BackendRoot 'obj\project.assets.json'),
        (Join-Path $Script:ProjectRoot 'LUMAR_ERP_API_V2.Tests\obj\project.assets.json')
    )
    $needsRestore = $Force -or (-not ($assets | Where-Object { Test-Path $_ }))

    if (-not $needsRestore) {
        Write-Host 'RESTORE_SKIPPED_REASON=Assets current; dependencies unchanged.'
        return 0
    }

    Write-Host 'RESTORE_REASON=Project assets are missing or forced refresh requested.'
    & dotnet restore $Script:SolutionPath --nologo
    if ($LASTEXITCODE -ne 0) {
        Write-StageResult -Name 'Restore' -Status 'FAIL' -Detail 'dotnet restore failed.'
        exit $LASTEXITCODE
    }

    Write-Host 'RESTORE_STATUS=PASS'
    return 0
}

function Invoke-FastBuild {
    Ensure-ToolDirectories
    $restoreMarker = Join-Path $Script:BackendRoot 'obj\project.assets.json'
    if ($Full) {
        Write-Host 'FULL_BUILD_SKIPPED_REASON=Full build requested by explicit flag; deterministic verification path.'
        & dotnet build $Script:BackendProject --configuration Debug --nologo
    }
    elseif (-not (Test-Path $restoreMarker)) {
        Write-Host 'FAST_BUILD_REASON=Restore required first; triggering a safe incremental build.'
        & dotnet build $Script:BackendProject --configuration Debug --nologo
    }
    else {
        Write-Host 'FULL_BUILD_SKIPPED_REASON=Incremental build sufficient for development loop.'
        & dotnet build $Script:BackendProject --configuration Debug --no-restore --nologo
    }

    if ($LASTEXITCODE -ne 0) {
        Write-StageResult -Name 'Build' -Status 'FAIL' -Detail 'Build failed.'
        exit $LASTEXITCODE
    }

    Write-Host 'BUILD_STATUS=PASS'
    return 0
}

function Invoke-FastTest {
    Ensure-ToolDirectories
    $argList = @('test', $Script:TestsProject, '--logger', 'console;verbosity=minimal')
    if ($Filter) {
        $argList += '--filter'
        $argList += $Filter
    }
    if ($NoBuild) { $argList += '--no-build' }
    else { $argList += '--no-restore' }

    Write-Host "TEST_FILTER=$Filter"
    & dotnet @argList
    if ($LASTEXITCODE -ne 0) {
        Write-StageResult -Name 'Test' -Status 'FAIL' -Detail "filter '$Filter' failed."
        exit $LASTEXITCODE
    }

    Write-Host 'TEST_STATUS=PASS'
    return 0
}

function Get-BackendPid {
    if (Test-Path $Script:RuntimePidPath) {
        $pidValue = (Get-Content $Script:RuntimePidPath -Raw).Trim()
        if (-not [string]::IsNullOrWhiteSpace($pidValue)) {
            try {
                $runtimePid = [int]$pidValue
                if ($runtimePid -gt 0) {
                    $process = Get-CimInstance Win32_Process -Filter "ProcessId = $runtimePid" -ErrorAction SilentlyContinue
                    if ($process) {
                        $cmdLine = $process.CommandLine
                        if ($cmdLine -and ($cmdLine -match 'LUMAR_ERP_API_V2|dotnet run|LUMAR_ERP_API_V2.csproj')) {
                            return $runtimePid
                        }
                    }
                }
            }
            catch {
            }
        }
    }

    $candidate = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object {
        $_.CommandLine -and $_.CommandLine -match 'LUMAR_ERP_API_V2|LUMAR_ERP_API_V2.csproj|dotnet run'
    } | Select-Object -ExpandProperty ProcessId -First 1

    if ($candidate) { return [int]$candidate }
    return $null
}

function Start-BackendRuntime {
    Ensure-ToolDirectories
    $existingPid = Get-BackendPid
    if ($existingPid) {
        $existingPort = Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object { $_.LocalPort -eq $Port } | Select-Object -ExpandProperty OwningProcess -First 1
        if ($existingPort -and $existingPort -eq $existingPid) {
            Write-Host 'RUNTIME_STATUS=ALREADY_RUNNING'
            return $existingPid
        }
    }

    $portTaken = Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object { $_.LocalPort -eq $Port }
    if ($portTaken) {
        throw "Port $Port already bound by another process; safe start aborted."
    }

    $proc = Start-Process -FilePath 'dotnet' -ArgumentList @('run', '--project', $Script:BackendProject, '--urls', "http://127.0.0.1:$Port") -WorkingDirectory $Script:BackendRoot -PassThru -RedirectStandardOutput $Script:RuntimeOutPath -RedirectStandardError $Script:RuntimeErrPath
    $proc.Id | Set-Content -Path $Script:RuntimePidPath
    Write-Host "RUNTIME_PID=$($proc.Id)"

    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline) {
        try {
            $resp = Invoke-WebRequest -Uri "http://127.0.0.1:$Port/swagger/index.html" -Method Get -UseBasicParsing -TimeoutSec 3
            if ($resp.StatusCode -ge 200 -and $resp.StatusCode -lt 500) {
                Write-Host 'RUNTIME_STATUS=PASS'
                return $proc.Id
            }
        }
        catch {
            Start-Sleep -Seconds 1
        }
    }

    Write-StageResult -Name 'Runtime start' -Status 'FAIL' -Detail "Backend on port $Port did not report ready within timeout."
    exit 1
}

function Stop-BackendRuntime {
    $backendPid = Get-BackendPid
    if (-not $backendPid) {
        $ownedCandidates = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object {
            $_.CommandLine -and $_.CommandLine -match 'LUMAR_ERP_API_V2|LUMAR_ERP_API_V2.csproj|dotnet run'
        }

        if (-not $ownedCandidates) {
            Write-Host 'STOP_STATUS=NO_PID_FOUND'
            return 0
        }

        foreach ($candidate in $ownedCandidates) {
            Stop-Process -Id $candidate.ProcessId -Force -ErrorAction SilentlyContinue
            Write-Host "STOP_STATUS=PASS PID=$($candidate.ProcessId)"
            Remove-Item -Path $Script:RuntimePidPath -Force -ErrorAction SilentlyContinue
        }
        return 0
    }

    $process = Get-CimInstance Win32_Process -Filter "ProcessId = $backendPid" -ErrorAction SilentlyContinue
    if (-not $process) {
        Write-Host 'STOP_STATUS=PID_NOT_FOUND'
        return 0
    }

    $cmdLine = $process.CommandLine
    if ($cmdLine -and ($cmdLine -match 'LUMAR_ERP_API_V2|LUMAR_ERP_API_V2.csproj')) {
        Stop-Process -Id $backendPid -Force
        Remove-Item -Path $Script:RuntimePidPath -Force -ErrorAction SilentlyContinue
        Write-Host "STOP_STATUS=PASS PID=$backendPid"
        return 0
    }

    Write-Host 'STOP_STATUS=REFUSED_UNOWNED_PID'
    return 1
}

function Invoke-Smoke {
    Ensure-ToolDirectories
    $backendPid = Get-BackendPid
    if (-not $backendPid) {
        $backendPid = Start-BackendRuntime -Port $Port
    }

    $dbOk = Test-SqlDatabaseIdentity -Database $Script:DefaultDatabase -Server $Script:DefaultServer
    $httpStatus = 0
    $httpOk = $false
    try {
        $resp = Invoke-WebRequest -Uri "http://127.0.0.1:$Port/swagger/index.html" -Method Get -UseBasicParsing -TimeoutSec 5
        $httpStatus = [int]$resp.StatusCode
        $httpOk = $httpStatus -ge 200 -and $httpStatus -lt 500
    }
    catch {
        $httpOk = $false
    }

    if (-not $dbOk -or -not $httpOk) {
        Write-Host 'FIRST_FAILURE=DB_OR_HTTP_NOT_READY'
        Write-Host "PORT=$Port"
        Write-Host "DATABASE=$($Script:DefaultDatabase)"
        Write-Host "HTTP_STATUS=$httpStatus"
        exit 1
    }

    Write-Host 'SMOKE_STATUS=PASS'
    Write-Host "HTTP_STATUS=$httpStatus"
    Write-Host "DATABASE=$($Script:DefaultDatabase)"
    Write-Host "PORT=$Port"
    Write-Host "LOG_PATH=$($Script:RuntimeOutPath)"
    return 0
}

function Invoke-DbVerify {
    $expected = Get-ExpectedDbGuard
    $dbOk = Test-SqlDatabaseIdentity -Database $expected -Server $Script:DefaultServer
    if (-not $dbOk) {
        Write-Host 'FIRST_FAILURE=DATABASE_IDENTITY_MISMATCH'
        Write-Host "EXPECTED_DATABASE=$expected"
        Write-Host 'ACTUAL_DATABASE=MISSING'
        exit 1
    }

    $query = @'
SET NOCOUNT ON;
SELECT DB_NAME() AS database_name,
       (SELECT COUNT(*) FROM sys.tables) AS table_count,
       (SELECT COUNT(*) FROM sys.objects WHERE type = 'PK') AS pk_count;
'@
    $result = & sqlcmd -S $Script:DefaultServer -d $expected -E -Q $query
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'DB_VERIFY_STATUS=FAIL'
        exit 1
    }

    Write-Host 'DB_VERIFY_STATUS=PASS'
    $result | ForEach-Object { Write-Host $_ }
    return 0
}

function Get-ProjectMap {
    return [ordered]@{
        projectId = 'LUMAR'
        solution = $Script:SolutionPath
        backend = $Script:BackendRoot
        backendEntryPoint = $Script:BackendProject
        dbContext = (Join-Path $Script:BackendRoot 'Data')
        repositories = (Join-Path $Script:BackendRoot 'Repositories')
        services = (Join-Path $Script:BackendRoot 'Services')
        controllers = (Join-Path $Script:BackendRoot 'Controllers')
        tests = $Script:TestsProject
        flutterRoot = $Script:FlutterRoot
        flutterServices = (Join-Path $Script:FlutterRoot 'lib\services')
        flutterScreens = (Join-Path $Script:FlutterRoot 'lib\screens')
        migrations = (Join-Path $Script:BackendRoot 'Migrations')
        settings = (Join-Path $Script:BackendRoot 'appsettings.json')
        verificationTools = $Script:VerificationRoot
        policies = 'D:\YASIN\Backend\policies'
    }
}

function Invoke-Flutter {
    Ensure-ToolDirectories
    if (-not (Test-Path $Script:FlutterRoot)) {
        Write-Host 'FLUTTER_STATUS=SKIPPED'
        Write-Host "FLUTTER_ROOT_MISSING=$($Script:FlutterRoot)"
        return 0
    }

    if (-not (Get-Command flutter -ErrorAction SilentlyContinue)) {
        Write-StageResult -Name 'Flutter verification' -Status 'FAIL' -Detail 'flutter not found in PATH.'
        exit 1
    }

    $start = Get-Date
    if (-not (Test-Path (Join-Path $Script:FlutterRoot 'pubspec.lock')) -or $Force) {
        Write-Host 'FLUTTER_RESTORE_REASON=pubspec lock missing or force requested.'
        & flutter pub get --directory $Script:FlutterRoot
        if ($LASTEXITCODE -ne 0) { Write-StageResult -Name 'Flutter restore' -Status 'FAIL' -Detail 'flutter pub get failed.'; exit $LASTEXITCODE }
    }

    & flutter analyze $Script:FlutterRoot --no-fatal-infos
    if ($LASTEXITCODE -ne 0) {
        Write-StageResult -Name 'Flutter' -Status 'FAIL' -Detail 'flutter analyze failed.'
        exit $LASTEXITCODE
    }

    $duration = [math]::Round(((Get-Date) - $start).TotalSeconds, 2)
    Write-Host "FLUTTER_STATUS=PASS"
    Write-Host "FLUTTER_DURATION=$duration"
    return 0
}

function Invoke-FastVerify {
    Invoke-Doctor
    Invoke-Restore
    Invoke-FastBuild
    Invoke-FastTest -Filter $Filter -NoBuild:$false
    Invoke-Smoke
    Invoke-DbVerify
    Write-Host 'FAST_VERIFY_STATUS=PASS'
}

function Invoke-FullVerify {
    Invoke-Doctor
    Invoke-Restore
    Invoke-FastBuild
    Invoke-FastTest -Filter $Filter -NoBuild:$false
    Invoke-Smoke
    Invoke-DbVerify
    Invoke-Flutter
    Write-Host 'FULL_VERIFY_STATUS=PASS'
}

switch ($Action) {
    'doctor' { Invoke-Doctor; break }
    'restore' { Invoke-Restore; break }
    'build' { Invoke-FastBuild; break }
    'test' { Invoke-FastTest -Filter $Filter -NoBuild:$NoBuild; break }
    'flutter' { Invoke-Flutter; break }
    'run' { Start-BackendRuntime -Port $Port; break }
    'stop' { Stop-BackendRuntime; break }
    'smoke' { Invoke-Smoke -Port $Port; break }
    'db' { Invoke-DbVerify; break }
    'db-verify' { Invoke-DbVerify; break }
    'verify' {
        if ($Full -or ($RemainingArgs -contains 'full')) { Invoke-FullVerify } else { Invoke-FastVerify }
        break
    }
    'verify-full' { Invoke-FullVerify; break }
    'map' {
        $map = Get-ProjectMap
        $map | ConvertTo-Json -Depth 10
        break
    }
    'json' {
        $path = Join-Path $Script:ProjectRoot 'tools\project-index.json'
        Get-Content $path -Raw
        break
    }
    'help' {
        Write-Host 'Usage: dev.ps1 [doctor|restore|build|test|flutter|run|stop|smoke|db|verify|map|json] [--full] [--filter "..."] [--port 5093]'
        break
    }
    default {
        Write-Host "Unsupported action: $Action"
        exit 1
    }
}
