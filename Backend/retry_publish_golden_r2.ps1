$ErrorActionPreference = 'Stop'

$sourceFile = 'D:\YASIN\Backend\LUMAR_ERP_VIP_1_CLEAN_R2_SCHEMA.dacpac'
$targetConnection = 'Data Source=YASIN-YASIN\SQLEXPRESS;Initial Catalog=LUMAR_ERP_GOLDEN_CLEAN_R2;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
$diagFile = 'D:\YASIN\Backend\golden_clean_r2_publish_diagnostics.log'
$outFile = 'D:\YASIN\Backend\golden_clean_r2_publish_stdout.txt'
$sqlPackage = 'D:\YASIN\Backend\SqlPackage\extract\sqlpackage.exe'

Write-Host "Starting SqlPackage Publish..."
Write-Host "SourceFile=$sourceFile"
Write-Host "TargetConnection=$targetConnection"
Write-Host "DiagnosticsFile=$diagFile"

& $sqlPackage /Action:Publish /SourceFile:$sourceFile /TargetConnectionString:$targetConnection /p:DropObjectsNotInSource=False /DiagnosticsFile:$diagFile 2>&1 | Tee-Object -FilePath $outFile
$ec = $LASTEXITCODE
Write-Host "SQLPACKAGE_EXIT=$ec"
exit $ec
