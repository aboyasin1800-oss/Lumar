@echo off
curl -I http://127.0.0.1:5093/
echo SQL_CHECK
sqlcmd -S "YASIN-YASIN\SQLEXPRESS" -d "LUMAR_ERP_ES_VALIDATION_R3" -Q "SELECT DB_NAME() AS DatabaseName, @@SERVERNAME AS ServerName;" -W -s ","
