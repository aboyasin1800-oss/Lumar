@echo off
setlocal enabledelayedexpansion
set "SQLSERVER=YASIN-YASIN\SQLEXPRESS"
set "DB=LUMAR_ERP_TEST_R3_ROLLS"
set "MIGRATIONS=D:\YASIN\Backend\LUMAR_ERP_API_V2\Migrations"

for /f "delims=" %%F in ('dir /b /o:n "%MIGRATIONS%\*.sql"') do (
  echo Applying %%F
  sqlcmd -S "%SQLSERVER%" -d "%DB%" -i "%MIGRATIONS%\%%F"
  if errorlevel 1 (
    echo Failed while applying %%F
    exit /b %errorlevel%
  )
)

echo All migrations applied to %DB%
