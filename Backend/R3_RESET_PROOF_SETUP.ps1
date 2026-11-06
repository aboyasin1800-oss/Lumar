$ErrorActionPreference = 'Stop'

$server = 'YASIN-YASIN\SQLEXPRESS'
$sourceDb = 'LUMAR_ERP_ES_VALIDATION_R3'
$proofDb = 'LUMAR_ERP_ES_VALIDATION_R3_RESET_PROOF'
$backupPath = 'D:\YASIN\Backend\R3_RESET_PROOF_BACKUP.bak'
$mdfPath = 'D:\SQLData\LUMAR_ERP_ES_VALIDATION_R3_RESET_PROOF.mdf'
$logPath = 'D:\SQLLogs\LUMAR_ERP_ES_VALIDATION_R3_RESET_PROOF_log.ldf'

New-Item -ItemType Directory -Force -Path 'D:\SQLData' | Out-Null
New-Item -ItemType Directory -Force -Path 'D:\SQLLogs' | Out-Null

$sql1 = @"
IF DB_ID('$proofDb') IS NOT NULL
BEGIN
    ALTER DATABASE [$proofDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [$proofDb];
END;
BACKUP DATABASE [$sourceDb] TO DISK='$backupPath' WITH INIT, COPY_ONLY, FORMAT, STATS = 5;
"@

& sqlcmd -S $server -E -h -1 -W -Q $sql1
if ($LASTEXITCODE -ne 0) { throw "Backup failed" }

$sql2 = @"
RESTORE DATABASE [$proofDb]
FROM DISK = '$backupPath'
WITH MOVE '$sourceDb' TO '$mdfPath',
     MOVE '$($sourceDb)_log' TO '$logPath',
     REPLACE, STATS = 5;
"@

& sqlcmd -S $server -E -h -1 -W -Q $sql2
if ($LASTEXITCODE -ne 0) { throw "Restore failed" }

Write-Host "Created isolated proof database: $proofDb"
