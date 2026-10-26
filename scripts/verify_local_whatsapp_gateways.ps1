$ErrorActionPreference = 'Stop'

$root = 'D:\YASIN'
$artifactDir = Join-Path $root 'artifacts\local-whatsapp-validation'
New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null

function Invoke-LoggedCommand {
    param(
        [Parameter(Mandatory = $true)] [string]$Label,
        [Parameter(Mandatory = $true)] [string]$FileName,
        [Parameter(Mandatory = $true)] [string[]]$Arguments,
        [Parameter()] [string]$WorkingDirectory = $root
    )

    $logPath = Join-Path $artifactDir ($Label + '.txt')
    Push-Location $WorkingDirectory
    try {
        $output = & $FileName @Arguments 2>&1
        $exitCode = $LASTEXITCODE
        $content = @(
            "=== LABEL: $Label ===",
            "=== COMMAND: $FileName $($Arguments -join ' ') ===",
            $output,
            "EXIT_CODE=$exitCode"
        ) -join [Environment]::NewLine
        [System.IO.File]::WriteAllText($logPath, $content)
        Write-Output $content
        return [pscustomobject]@{ Label = $Label; ExitCode = $exitCode; Log = $logPath }
    }
    finally {
        Pop-Location
    }
}

$results = @()

$results += Invoke-LoggedCommand -Label 'dotnet-version' -FileName 'dotnet' -Arguments @('--info')
$results += Invoke-LoggedCommand -Label 'backend-build' -FileName 'dotnet' -Arguments @('build', 'Backend\LUMAR_ERP_API_V2\LUMAR_ERP_API_V2.csproj', '--nologo', '-v', 'minimal')

$focusedFilter = 'FullyQualifiedName~LocalWhatsAppFoundationTests|FullyQualifiedName~MessageWriteServiceTests'
$results += Invoke-LoggedCommand -Label 'local-whatsapp-focused-tests' -FileName 'dotnet' -Arguments @('test', 'Backend\LUMAR_ERP_API_V2.Tests\LUMAR_ERP_API_V2.Tests.csproj', '--filter', $focusedFilter, '--nologo', '-v', 'minimal', '--logger', 'trx;LogFileName=local-whatsapp-focused.trx', '--results-directory', 'artifacts\local-whatsapp-validation')

$results += Invoke-LoggedCommand -Label 'gateway-tests' -FileName 'node' -Arguments @('--test', 'Gateway\LocalWhatsApp\test\*.test.mjs', '--test-reporter=spec', '--test-reporter-destination=stdout')

Write-Output '=== SUMMARY ==='
$results | ForEach-Object {
    Write-Output ("{0}: EXIT_CODE={1} LOG={2}" -f $_.Label, $_.ExitCode, $_.Log)
}

if (($results | Where-Object { $_.ExitCode -ne 0 }).Count -gt 0) {
    exit 1
}
