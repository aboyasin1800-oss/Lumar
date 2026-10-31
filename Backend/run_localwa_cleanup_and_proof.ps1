$procs = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
    Where-Object { ($_.Name -eq 'LUMAR_ERP_API_V2.exe' -or $_.Name -eq 'testhost.exe') -and $_.CommandLine -match 'LUMAR_ERP_API_V2.Tests|LUMAR_ERP_API_V2' }

if ($procs) {
    foreach ($proc in $procs) {
        Stop-Process -Id $proc.ProcessId -Force -ErrorAction SilentlyContinue
        Write-Host "KILL:$($proc.ProcessId):$($proc.Name)"
    }
} else {
    Write-Host 'NO_PROJECT_MATCHES'
}

Set-Location 'D:\YASIN\Backend'
New-Item -ItemType Directory -Force -Path '.\test-results' | Out-Null
dotnet test '.\LUMAR_ERP_API_V2.Tests\LUMAR_ERP_API_V2.Tests.csproj' --filter 'LocalWhatsAppFoundationTests' --nologo --logger 'trx;LogFileName=localwa_foundation_exact.trx' --results-directory '.\test-results'
