Get-Process -Name tailoring_system -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process -Name LUMAR_ERP_API_V2 -ErrorAction SilentlyContinue | Stop-Process -Force
Write-Host 'تم إيقاف تطبيق Windows وBackend المحليين.'