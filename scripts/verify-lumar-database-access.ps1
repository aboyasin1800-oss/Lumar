# هذا السكربت للتحقق من الاتصال والقراءة فقط، ولا ينفذ أي تعديل على قاعدة البيانات.
# لا يكتب بيانات، ولا يغير أي إعدادات تشغيلية، ولا يضيف كلمات مرور أو معلومات سرية.

& "C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\SQLCMD.EXE" `
  -S "YASIN-YASIN\SQLEXPRESS" `
  -d "LUMAR_ERP" `
  -E `
  -C `
  -b `
  -r 1 `
  -l 15 `
  -Q "SET NOCOUNT ON; SELECT @@SERVERNAME AS ServerName, DB_NAME() AS DatabaseName, SYSTEM_USER AS ConnectedUser, GETDATE() AS ServerTime;"

Write-Host "SQLCMD_EXIT=$LASTEXITCODE"
