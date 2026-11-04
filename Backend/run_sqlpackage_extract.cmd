@echo off
"D:\YASIN\Backend\SqlPackage\extract\sqlpackage.exe" /Action:Extract /SourceConnectionString:"Data Source=YASIN-YASIN\SQLEXPRESS;Initial Catalog=LUMAR_ERP_VIP_1;Integrated Security=True;Encrypt=True;TrustServerCertificate=True" /TargetFile:"D:\YASIN\Backend\LUMAR_ERP_VIP_1_CLEAN_R2_SCHEMA.dacpac" /p:ExtractAllTableData=false
exit /b %ERRORLEVEL%
