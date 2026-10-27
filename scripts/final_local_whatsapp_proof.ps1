$ErrorActionPreference = 'Stop'

Set-Location 'D:\YASIN'

function Invoke-Check {
    param(
        [string]$Label,
        [string]$Command,
        [string[]]$Args
    )

    Write-Host "=== $Label ==="
    & $Command @Args
    $exit = $LASTEXITCODE
    Write-Host "EXIT_CODE=$exit"
    if ($exit -ne 0) {
        throw "FAILED: $Label exit=$exit"
    }
}

Invoke-Check -Label 'Gateway Session Test' -Command 'node' -Args @('--test', 'Gateway\LocalWhatsApp\test\session.test.mjs')
Invoke-Check -Label 'Gateway Full Suite' -Command 'node' -Args @('--test', 'Gateway\LocalWhatsApp\test\*.test.mjs')
Invoke-Check -Label 'Backend Build' -Command 'dotnet' -Args @('build', 'Backend\LUMAR_ERP_API_V2\LUMAR_ERP_API_V2.csproj', '--nologo', '--disable-build-servers')
Invoke-Check -Label 'Backend Local WhatsApp Tests' -Command 'dotnet' -Args @('test', 'Backend\LUMAR_ERP_API_V2.Tests\LUMAR_ERP_API_V2.Tests.csproj', '--filter', 'LocalWhatsAppFoundationTests', '--nologo', '--logger', 'console;verbosity=minimal')

Write-Host 'ALL_CHECKS_PASSED'
