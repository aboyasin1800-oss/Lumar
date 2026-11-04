$ErrorActionPreference = 'Stop'

$server = 'YASIN-YASIN\SQLEXPRESS'
$database = 'LUMAR_ERP_GOLDEN_CLEAN_R2'
$sourceFile = 'D:\YASIN\Backend\LUMAR_ERP_VIP_1_CLEAN_R2_SCHEMA.dacpac'
$diagFile = 'D:\YASIN\Backend\golden_clean_r2_publish_diagnostics.log'
$outFile = 'D:\YASIN\Backend\golden_clean_r2_publish_stdout.txt'
$sqlPackage = 'D:\YASIN\Backend\SqlPackage\extract\sqlpackage.exe'

Write-Host "Dropping target database if present: $database"
$sqlcmd = (Get-Command sqlcmd -ErrorAction SilentlyContinue).Source
if (-not $sqlcmd) {
    $sqlcmd = 'C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\sqlcmd.exe'
}

& $sqlcmd -S $server -d master -Q "IF DB_ID('$database') IS NOT NULL BEGIN ALTER DATABASE [$database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$database]; END;" -b
$dropExit = $LASTEXITCODE
Write-Host "DROP_EXIT=$dropExit"
if ($dropExit -ne 0) { exit $dropExit }

Write-Host "Starting SqlPackage Publish..."
& $sqlPackage /Action:Publish /SourceFile:$sourceFile /TargetConnectionString:"Data Source=$server;Initial Catalog=$database;Integrated Security=True;Encrypt=True;TrustServerCertificate=True" /p:DropObjectsNotInSource=False /DiagnosticsFile:$diagFile 2>&1 | Tee-Object -FilePath $outFile
$ec = $LASTEXITCODE
Write-Host "SQLPACKAGE_EXIT=$ec"
exit $ec
