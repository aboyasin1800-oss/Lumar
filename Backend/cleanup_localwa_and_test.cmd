@echo off
powershell -NoProfile -ExecutionPolicy Bypass -Command "$procs = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object { ($_.Name -eq 'LUMAR_ERP_API_V2.exe' -or $_.Name -eq 'testhost.exe') -and $_.CommandLine -match 'LUMAR_ERP_API_V2.Tests|LUMAR_ERP_API_V2' }; if ($procs) { $procs | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue; Write-Host \"KILL:$($_.ProcessId):$($_.Name)\" } } else { Write-Host 'NO_PROJECT_MATCHES' }"
cd /d D:\YASIN\Backend
dotnet test ".\LUMAR_ERP_API_V2.Tests\LUMAR_ERP_API_V2.Tests.csproj" --filter "LocalWhatsAppFoundationTests" --nologo --logger "console;verbosity=minimal"
