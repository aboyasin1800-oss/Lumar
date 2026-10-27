$ErrorActionPreference = 'Stop'

$root = 'D:\YASIN'
Set-Location $root

function Invoke-Check {
    param(
        [Parameter(Mandatory = $true)] [string]$Label,
        [Parameter(Mandatory = $true)] [string]$Command,
        [Parameter(Mandatory = $true)] [string[]]$Args
    )

    Write-Host "=== $Label ==="
    & $Command @Args
    $exitCode = $LASTEXITCODE
    Write-Host "EXIT_CODE=$exitCode"
    if ($exitCode -ne 0) {
        throw "Check failed: $Label (exit $exitCode)"
    }
}

Invoke-Check -Label 'Gateway Session Test' -Command 'node' -Args @('--test', 'Gateway\LocalWhatsApp\test\session.test.mjs')
Invoke-Check -Label 'Gateway Full Suite' -Command 'node' -Args @('--test', 'Gateway\LocalWhatsApp\test\*.test.mjs')
Invoke-Check -Label 'Backend Build' -Command 'dotnet' -Args @('build', 'Backend\LUMAR_ERP_API_V2\LUMAR_ERP_API_V2.csproj', '--nologo', '--disable-build-servers')
Invoke-Check -Label 'Backend Local WhatsApp Tests' -Command 'dotnet' -Args @('test', 'Backend\LUMAR_ERP_API_V2.Tests\LUMAR_ERP_API_V2.Tests.csproj', '--filter', 'LocalWhatsAppFoundationTests', '--nologo', '--logger', 'console;verbosity=minimal')

Write-Host 'ALL_CHECKS_PASSED'
