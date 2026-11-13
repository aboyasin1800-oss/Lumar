$server = 'YASIN-YASIN\SQLEXPRESS'
$db = 'LUMAR_ERP_ES_VALIDATION_R3'
$tables = @(
  'Customers','Suppliers','Employees','Orders','OrderItems','Pieces','Payments','AccountingEvents','CustomerAdvanceApplications','GoodsReceipts','MobileAccounts','MobileSessions','MobilePushDevices','MobileRecoveryChallenges','JournalEntries','JournalEntryLines','Users','UserSessions','MessageTemplates','System_Settings','LedgerAccounts','CashAccounts','PieceWageRates'
)
$nonZero = @()
foreach ($table in $tables) {
  $query = "SELECT '$table' AS TableName, COUNT_BIG(*) AS RowCount FROM [$table]"
  try {
    $result = Invoke-Sqlcmd -ServerInstance $server -Database $db -Query $query -SuppressProviderContextWarning -ErrorAction Stop
    $count = [long]$result.RowCount
    if ($count -gt 0) {
      $nonZero += "$table=$count"
    }
    Write-Host "$table=$count"
  } catch {
    Write-Host "$table=MISSING"
  }
}
if ($nonZero.Count -gt 0) { Write-Host "NONZERO_OPERATIONAL_TABLES_AFTER=$($nonZero -join ';')" } else { Write-Host 'NONZERO_OPERATIONAL_TABLES_AFTER=NONE' }
