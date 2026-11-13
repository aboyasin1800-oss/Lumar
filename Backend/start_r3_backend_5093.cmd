@echo off
setlocal
set "Lumar__ConnectionString=Server=YASIN-YASIN\SQLEXPRESS;Database=LUMAR_ERP_ES_VALIDATION_R3;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
cd /d "D:\YASIN\Backend\LUMAR_ERP_API_V2"
dotnet run --project ".\LUMAR_ERP_API_V2.csproj" --urls http://127.0.0.1:5093
endlocal
