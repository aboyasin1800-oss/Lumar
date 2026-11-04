$ErrorActionPreference = 'Stop'

$sourceFile = 'D:\YASIN\Backend\LUMAR_ERP_VIP_1_CLEAN_R2_SCHEMA.dacpac'
$targetConnection = 'Data Source=YASIN-YASIN\SQLEXPRESS;Initial Catalog=LUMAR_ERP_GOLDEN_CLEAN_R2;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
$diagFile = 'D:\YASIN\Backend\golden_clean_r2_publish_diagnostics_timeoutfix.log'
$outFile = 'D:\YASIN\Backend\golden_clean_r2_publish_stdout_timeoutfix.txt'
$sqlPackage = 'D:\YASIN\Backend\SqlPackage\extract\sqlpackage.exe'

Write-Host 'Starting SqlPackage Publish with extended timeout values...'
Write-Host "SourceFile=$sourceFile"
Write-Host "TargetConnection=$targetConnection"
Write-Host "DiagnosticsFile=$diagFile"

& $sqlPackage /Action:Publish /SourceFile:$sourceFile /TargetConnectionString:$targetConnection /p:DropObjectsNotInSource=False /p:CommandTimeout=0 /p:LongRunningCommandTimeout=0 /DiagnosticsFile:$diagFile 2>&1 | Tee-Object -FilePath $outFile
$ec = $LASTEXITCODE
Write-Host "EXIT_CODE=$ec"
if ($ec -ne 0) {
    exit $ec
}

# Prove a schema is present after successful publish.
$sql = @'
SELECT COUNT(*) FROM sys.tables;
'@
$connection = New-Object System.Data.SqlClient.SqlConnection($targetConnection)
$connection.Open()
try {
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = $sql
    $cmd.CommandTimeout = 0
    $count = [int]$cmd.ExecuteScalar()
    Write-Host "TABLE_COUNT=$count"
}
finally {
    $connection.Close()
}

exit 0
