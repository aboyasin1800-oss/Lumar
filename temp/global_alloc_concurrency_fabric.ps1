$server = 'YASIN-YASIN\SQLEXPRESS'
$db = 'LUMAR_ERP_TEST_R3_ROLLS'
$file = 'D:\YASIN\temp\global_alloc_concurrency_fabric.txt'
if (Test-Path $file) { Remove-Item $file -Force }
$jobs = New-Object System.Collections.Generic.List[System.Management.Automation.PSRemotingJob]
for ($i = 1; $i -le 20; $i++) {
    $jobs.Add((Start-Job -ScriptBlock {
        param($s, $db, $path)
        $sql = "BEGIN TRAN; UPDATE dbo.GlobalCodeAllocations WITH (UPDLOCK,HOLDLOCK) SET CurrentValue = CurrentValue + 1, UpdatedAt = SYSUTCDATETIME() OUTPUT INSERTED.AllocationScope, INSERTED.CurrentValue WHERE AllocationScope = 'FabricCode'; COMMIT;"
        $result = Invoke-Sqlcmd -ServerInstance $s -Database $db -Query $sql -TrustServerCertificate -QueryTimeout 120
        if ($result) {
            $result | ForEach-Object { $_.AllocationScope + ':' + $_.CurrentValue } | Out-File -Append -FilePath $path
        }
    } -ArgumentList $server, $db, $file))
}
Wait-Job -Job $jobs | Out-Null
Receive-Job -Job $jobs | Out-Null
if (Test-Path $file) { Get-Content $file | Select-Object -First 10 }
