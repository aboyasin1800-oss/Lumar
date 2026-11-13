$ErrorActionPreference = 'Stop'
$server = 'YASIN-YASIN\SQLEXPRESS'
$db = 'LUMAR_ERP_ES_VALIDATION_R3'
$tableSet = @(
  'Customers','Suppliers','Employees','Orders','OrderItems','Pieces','Payments','AccountingEvents','CustomerAdvanceApplications','GoodsReceipts','MobileAccounts','MobileSessions','MobilePushDevices','MobileRecoveryChallenges','JournalEntries','JournalEntryLines'
)
$nonZero = @()
foreach ($table in $tableSet) {
  $q = "SELECT COUNT_BIG(*) AS TotalRows FROM [$table]"
  $row = Invoke-Sqlcmd -ServerInstance $server -Database $db -Query $q -SuppressProviderContextWarning -ErrorAction Stop
  $count = [long]$row.TotalRows
  Write-Host "$table=$count"
  if ($count -gt 0) { $nonZero += "$table=$count" }
}
if ($nonZero.Count -eq 0) { Write-Host 'NONZERO_OPERATIONAL_TABLES_AFTER=NONE' } else { Write-Host "NONZERO_OPERATIONAL_TABLES_AFTER=$($nonZero -join ';')" }
