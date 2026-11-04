param(
    [string]$ConnectionString
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-ReadOnlyQueryResult {
    param(
        [Parameter(Mandatory = $true)][string]$Marker,
        [Parameter(Mandatory = $true)][string]$Sql
    )

    Write-Host ""
    Write-Host "=== $Marker ==="

    $resultSets = Invoke-ReadOnlySql -Sql $Sql
    if ($null -eq $resultSets -or $resultSets.Count -eq 0) {
        Write-Host "No rows returned."
        return
    }

    foreach ($resultSet in $resultSets) {
        if ($null -eq $resultSet -or $resultSet.Count -eq 0) {
            continue
        }
        $resultSet | Format-Table -AutoSize
    }
}

function New-DefaultConnectionString {
    $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    $builder.DataSource = 'YASIN-YASIN\SQLEXPRESS'
    $builder.InitialCatalog = 'LUMAR_ERP_ES_VALIDATION'
    $builder.IntegratedSecurity = $true
    $builder.MultipleActiveResultSets = $true
    $builder.TrustServerCertificate = $true
    return $builder.ConnectionString
}

function Invoke-ReadOnlySql {
    param(
        [Parameter(Mandatory = $true)][string]$Sql
    )

    $command = New-Object System.Data.SqlClient.SqlCommand($Sql, $script:SqlConnection)
    $command.CommandTimeout = 120
    $reader = $command.ExecuteReader()

    $results = New-Object System.Collections.Generic.List[object]
    do {
        $rows = New-Object System.Collections.Generic.List[object]
        while ($reader.Read()) {
            $row = [ordered]@{}
            for ($i = 0; $i -lt $reader.FieldCount; $i++) {
                $fieldName = $reader.GetName($i)
                $fieldValue = $reader.IsDBNull($i) ? $null : $reader.GetValue($i)
                $row[$fieldName] = $fieldValue
            }
            $rows.Add([pscustomobject]$row)
        }

        if ($rows.Count -gt 0) {
            $results.Add($rows)
        }
    } while ($reader.NextResult())

    $reader.Close()
    return $results
}

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    $ConnectionString = $env:Lumar__ConnectionString
}

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    $ConnectionString = New-DefaultConnectionString
}

$script:SqlConnection = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
$script:SqlConnection.Open()

try {
    $dbName = [string](New-Object System.Data.SqlClient.SqlCommand('SELECT DB_NAME();', $script:SqlConnection)).ExecuteScalar()

    Write-Host '=== DB_IDENTITY ==='
    Write-Host "DatabaseName=$dbName"
    Write-Host "ServerName=$($script:SqlConnection.DataSource)"
    Write-Host "IntegratedSecurity=$($script:SqlConnection.ConnectionString.Contains('Integrated Security=True'))"

    if ($dbName -ne 'LUMAR_ERP_ES_VALIDATION') {
        Write-Host 'FAIL-CLOSED: DB_NAME() is not LUMAR_ERP_ES_VALIDATION. Exiting without running any additional checks.'
        exit 1
    }

    $cutoverSql = @'
SELECT CAST(value AS nvarchar(128)) AS CutoverValueRaw,
       TRY_CONVERT(datetime2(7), CAST(value AS nvarchar(128))) AS CutoverUtc
FROM fn_listextendedproperty(N'CashMovementFoundationCutoverUtc', N'SCHEMA', N'dbo', N'TABLE', N'CashMovements', NULL, NULL);
'@
    Write-ReadOnlyQueryResult -Marker 'CASH_CUTOVER' -Sql $cutoverSql

    $cashTotalSql = @'
SELECT COUNT(*) AS TotalCashMovements,
       COALESCE(SUM(CASE WHEN CashDirection = 1 THEN Amount WHEN CashDirection = 2 THEN -Amount ELSE 0 END), 0) AS NetCashMovementBalance,
       COALESCE(SUM(Amount), 0) AS GrossCashMovementAmount
FROM dbo.CashMovements;
'@
    Write-ReadOnlyQueryResult -Marker 'CASH_MOVEMENT_TOTAL' -Sql $cashTotalSql

    $cashGoLiveSql = @'
DECLARE @cutoverUtc datetime2(7) = TRY_CONVERT(datetime2(7),
    (SELECT CAST(value AS nvarchar(128))
     FROM fn_listextendedproperty(N'CashMovementFoundationCutoverUtc', N'SCHEMA', N'dbo', N'TABLE', N'CashMovements', NULL, NULL)));

SELECT COUNT(*) AS GoLiveCashMovementCount,
       COALESCE(SUM(CASE WHEN CashDirection = 1 THEN Amount WHEN CashDirection = 2 THEN -Amount ELSE 0 END), 0) AS GoLiveNetBalance,
       COALESCE(SUM(Amount), 0) AS GoLiveGrossAmount
FROM dbo.CashMovements
WHERE CreatedAt >= @cutoverUtc;
'@
    Write-ReadOnlyQueryResult -Marker 'CASH_GOLIVE_TOTAL' -Sql $cashGoLiveSql

    $cash148Vs146Sql = @'
DECLARE @cutoverUtc datetime2(7) = TRY_CONVERT(datetime2(7),
    (SELECT CAST(value AS nvarchar(128))
     FROM fn_listextendedproperty(N'CashMovementFoundationCutoverUtc', N'SCHEMA', N'dbo', N'TABLE', N'CashMovements', NULL, NULL)));

WITH GoLiveCash AS (
    SELECT CashMovementId, CashAccountId, AccountingEventId, CashDirection, Amount, CreatedAt
    FROM dbo.CashMovements
    WHERE CreatedAt >= @cutoverUtc
)
SELECT COUNT(*) AS GoLiveRowCount,
       COUNT(DISTINCT AccountingEventId) AS DistinctAccountingEvents,
       SUM(CASE WHEN AccountingEventId IS NULL THEN 1 ELSE 0 END) AS RowsMissingAccountingEvent
FROM GoLiveCash;

SELECT TOP (200) CashMovementId, CashAccountId, AccountingEventId, CashDirection, Amount, CreatedAt
FROM dbo.CashMovements
WHERE CreatedAt >= @cutoverUtc
ORDER BY CreatedAt, CashMovementId;
'@
    Write-ReadOnlyQueryResult -Marker 'CASH_148_VS_146' -Sql $cash148Vs146Sql

    $cashRecalc507557Sql = @'
DECLARE @cutoverUtc datetime2(7) = TRY_CONVERT(datetime2(7),
    (SELECT CAST(value AS nvarchar(128))
     FROM fn_listextendedproperty(N'CashMovementFoundationCutoverUtc', N'SCHEMA', N'dbo', N'TABLE', N'CashMovements', NULL, NULL)));

SELECT SUM(CASE WHEN CashDirection = 1 THEN Amount WHEN CashDirection = 2 THEN -Amount ELSE 0 END) AS RecalculatedCashMovementBalance
FROM dbo.CashMovements
WHERE CreatedAt >= @cutoverUtc;

SELECT TOP (200) CashMovementId, CashDirection, Amount,
       CASE WHEN CashDirection = 1 THEN Amount WHEN CashDirection = 2 THEN -Amount ELSE 0 END AS SignedAmount,
       CreatedAt, AccountingEventId
FROM dbo.CashMovements
WHERE CreatedAt >= @cutoverUtc
ORDER BY CreatedAt, CashMovementId;
'@
    Write-ReadOnlyQueryResult -Marker 'CASH_RECALC_507557_50' -Sql $cashRecalc507557Sql

    $financialFlowSql = @'
SELECT TransactionType,
       COUNT(*) AS RowCount,
       COALESCE(SUM(Amount), 0) AS TotalAmount
FROM dbo.FinancialTransactions
GROUP BY TransactionType
ORDER BY TransactionType;

SELECT COALESCE(SUM(Amount), 0) AS TotalFinancialTransactionAmount
FROM dbo.FinancialTransactions;
'@
    Write-ReadOnlyQueryResult -Marker 'FINANCIAL_TRANSACTION_CASH_FLOW' -Sql $financialFlowSql

    $cashDelta6955Sql = @'
DECLARE @cutoverUtc datetime2(7) = TRY_CONVERT(datetime2(7),
    (SELECT CAST(value AS nvarchar(128))
     FROM fn_listextendedproperty(N'CashMovementFoundationCutoverUtc', N'SCHEMA', N'dbo', N'TABLE', N'CashMovements', NULL, NULL)));

SELECT
    (SELECT COALESCE(SUM(CASE cm.CashDirection WHEN 1 THEN cm.Amount WHEN 2 THEN -cm.Amount ELSE 0 END), 0)
     FROM dbo.CashMovements cm
     WHERE cm.CreatedAt >= @cutoverUtc) AS CashMovementsNet,
    (SELECT COALESCE(SUM(jel.DebitAmount - jel.CreditAmount), 0)
     FROM dbo.JournalEntryLines jel
     INNER JOIN dbo.JournalEntries je ON je.JournalEntryId = jel.JournalEntryId
     INNER JOIN dbo.AccountingEvents ae ON ae.AccountingEventId = je.AccountingEventId
     INNER JOIN dbo.CashMovements cm ON cm.AccountingEventId = ae.AccountingEventId
     INNER JOIN dbo.LedgerAccounts la ON la.LedgerAccountId = jel.LedgerAccountId
     WHERE la.AccountCode = N'1000' AND cm.CreatedAt >= @cutoverUtc) AS GeneralLedgerNet,
    ((SELECT COALESCE(SUM(CASE cm.CashDirection WHEN 1 THEN cm.Amount WHEN 2 THEN -cm.Amount ELSE 0 END), 0)
      FROM dbo.CashMovements cm
      WHERE cm.CreatedAt >= @cutoverUtc)
     -
     (SELECT COALESCE(SUM(jel.DebitAmount - jel.CreditAmount), 0)
      FROM dbo.JournalEntryLines jel
      INNER JOIN dbo.JournalEntries je ON je.JournalEntryId = jel.JournalEntryId
      INNER JOIN dbo.AccountingEvents ae ON ae.AccountingEventId = je.AccountingEventId
      INNER JOIN dbo.CashMovements cm ON cm.AccountingEventId = ae.AccountingEventId
      INNER JOIN dbo.LedgerAccounts la ON la.LedgerAccountId = jel.LedgerAccountId
      WHERE la.AccountCode = N'1000' AND cm.CreatedAt >= @cutoverUtc)) AS Difference;

SELECT TOP (200)
    cm.CashMovementId,
    cm.AccountingEventId,
    cm.CashDirection,
    cm.Amount,
    CASE WHEN cm.CashDirection = 1 THEN cm.Amount WHEN cm.CashDirection = 2 THEN -cm.Amount ELSE 0 END AS SignedCashAmount,
    ae.AccountingEventId AS EventId,
    je.JournalEntryId,
    la.AccountCode,
    jel.DebitAmount,
    jel.CreditAmount,
    (jel.DebitAmount - jel.CreditAmount) AS JournalLineNet
FROM dbo.CashMovements cm
LEFT JOIN dbo.AccountingEvents ae ON ae.AccountingEventId = cm.AccountingEventId
LEFT JOIN dbo.JournalEntries je ON je.AccountingEventId = ae.AccountingEventId
LEFT JOIN dbo.JournalEntryLines jel ON jel.JournalEntryId = je.JournalEntryId
LEFT JOIN dbo.LedgerAccounts la ON la.LedgerAccountId = jel.LedgerAccountId
WHERE cm.CreatedAt >= @cutoverUtc
ORDER BY cm.CreatedAt, cm.CashMovementId;
'@
    Write-ReadOnlyQueryResult -Marker 'CASH_DELTA_6955' -Sql $cashDelta6955Sql

    $ledger1000Sql = @'
SELECT la.AccountCode,
       la.AccountName,
       COALESCE(SUM(jel.DebitAmount), 0) AS DebitTotal,
       COALESCE(SUM(jel.CreditAmount), 0) AS CreditTotal,
       COALESCE(SUM(jel.DebitAmount - jel.CreditAmount), 0) AS NetBalance
FROM dbo.LedgerAccounts la
LEFT JOIN dbo.JournalEntryLines jel ON jel.LedgerAccountId = la.LedgerAccountId
WHERE la.AccountCode = N'1000'
GROUP BY la.AccountCode, la.AccountName;

SELECT TOP (200) je.JournalEntryId, je.ReferenceNumber, je.AccountingEventId, la.AccountCode, jel.JournalEntryLineId, jel.DebitAmount, jel.CreditAmount, (jel.DebitAmount - jel.CreditAmount) AS LineNet
FROM dbo.JournalEntryLines jel
INNER JOIN dbo.LedgerAccounts la ON la.LedgerAccountId = jel.LedgerAccountId
INNER JOIN dbo.JournalEntries je ON je.JournalEntryId = jel.JournalEntryId
WHERE la.AccountCode = N'1000'
ORDER BY je.JournalEntryId, jel.JournalEntryLineId;
'@
    Write-ReadOnlyQueryResult -Marker 'LEDGER_1000' -Sql $ledger1000Sql

    $cashDelta500463Sql = @'
SELECT TransactionType,
       COUNT(*) AS RowCount,
       COALESCE(SUM(Amount), 0) AS TotalAmount
FROM dbo.FinancialTransactions
GROUP BY TransactionType
HAVING ABS(COALESCE(SUM(Amount), 0)) > 0
ORDER BY TotalAmount DESC;

SELECT COALESCE(SUM(Amount), 0) AS TotalFinancialTransactionsAmount
FROM dbo.FinancialTransactions;
'@
    Write-ReadOnlyQueryResult -Marker 'CASH_DELTA_500463' -Sql $cashDelta500463Sql

    $cashWithoutJournalSql = @'
DECLARE @cutoverUtc datetime2(7) = TRY_CONVERT(datetime2(7),
    (SELECT CAST(value AS nvarchar(128))
     FROM fn_listextendedproperty(N'CashMovementFoundationCutoverUtc', N'SCHEMA', N'dbo', N'TABLE', N'CashMovements', NULL, NULL)));

SELECT cm.CashMovementId, cm.CashAccountId, cm.AccountingEventId, cm.CashDirection, cm.Amount, cm.CreatedAt
FROM dbo.CashMovements cm
LEFT JOIN dbo.AccountingEvents ae ON ae.AccountingEventId = cm.AccountingEventId
LEFT JOIN dbo.JournalEntries je ON je.AccountingEventId = ae.AccountingEventId
WHERE cm.CreatedAt >= @cutoverUtc
  AND (cm.AccountingEventId IS NULL OR je.JournalEntryId IS NULL)
ORDER BY cm.CreatedAt, cm.CashMovementId;

SELECT COUNT(*) AS CashMovementsWithoutAccountingEvent
FROM dbo.CashMovements cm
WHERE cm.AccountingEventId IS NULL;

SELECT COUNT(*) AS CashMovementsWithoutJournalEntry
FROM dbo.CashMovements cm
LEFT JOIN dbo.AccountingEvents ae ON ae.AccountingEventId = cm.AccountingEventId
LEFT JOIN dbo.JournalEntries je ON je.AccountingEventId = ae.AccountingEventId
WHERE cm.AccountingEventId IS NOT NULL AND je.JournalEntryId IS NULL;
'@
    Write-ReadOnlyQueryResult -Marker 'CASH_WITHOUT_JOURNAL' -Sql $cashWithoutJournalSql

    $journalWithoutCashSql = @'
DECLARE @cutoverUtc datetime2(7) = TRY_CONVERT(datetime2(7),
    (SELECT CAST(value AS nvarchar(128))
     FROM fn_listextendedproperty(N'CashMovementFoundationCutoverUtc', N'SCHEMA', N'dbo', N'TABLE', N'CashMovements', NULL, NULL)));

SELECT je.JournalEntryId, je.ReferenceNumber, je.AccountingEventId, je.EntryDate,
       SUM(CASE WHEN la.AccountCode = N'1000' THEN jel.DebitAmount - jel.CreditAmount ELSE 0 END) AS Net1000Balance,
       COUNT(cm.CashMovementId) AS LinkedCashMovements
FROM dbo.JournalEntries je
LEFT JOIN dbo.JournalEntryLines jel ON jel.JournalEntryId = je.JournalEntryId
LEFT JOIN dbo.LedgerAccounts la ON la.LedgerAccountId = jel.LedgerAccountId
LEFT JOIN dbo.CashMovements cm ON cm.AccountingEventId = je.AccountingEventId
WHERE je.EntryDate >= @cutoverUtc
GROUP BY je.JournalEntryId, je.ReferenceNumber, je.AccountingEventId, je.EntryDate
HAVING COUNT(cm.CashMovementId) = 0
ORDER BY je.EntryDate, je.JournalEntryId;
'@
    Write-ReadOnlyQueryResult -Marker 'JOURNAL_WITHOUT_CASH' -Sql $journalWithoutCashSql

    $duplicatesSql = @'
SELECT 'CashMovements' AS EntityName, CashMovementId AS DuplicateIdentity, COUNT(*) AS DuplicateCount
FROM dbo.CashMovements
GROUP BY CashMovementId
HAVING COUNT(*) > 1;

SELECT 'FinancialTransactions' AS EntityName, ReferenceNumber AS DuplicateIdentity, COUNT(*) AS DuplicateCount
FROM dbo.FinancialTransactions
GROUP BY ReferenceNumber
HAVING COUNT(*) > 1;

SELECT 'JournalEntries' AS EntityName, ReferenceNumber AS DuplicateIdentity, COUNT(*) AS DuplicateCount
FROM dbo.JournalEntries
GROUP BY ReferenceNumber
HAVING COUNT(*) > 1;

SELECT 'AccountingEvents' AS EntityName, AccountingEventId AS DuplicateIdentity, COUNT(*) AS DuplicateCount
FROM dbo.AccountingEvents
GROUP BY AccountingEventId
HAVING COUNT(*) > 1;
'@
    Write-ReadOnlyQueryResult -Marker 'DUPLICATES' -Sql $duplicatesSql

    $amountMismatchesSql = @'
SELECT ft.FinancialTransactionId,
       ft.ReferenceNumber,
       ft.TransactionType,
       ft.Amount AS FinancialTransactionAmount,
       ae.AccountingEventId,
       ae.PostingAmount AS AccountingEventAmount,
       cm.CashMovementId,
       cm.CashDirection,
       cm.Amount AS CashMovementAmount,
       SUM(CASE WHEN la.AccountCode = N'1000' THEN jel.DebitAmount - jel.CreditAmount ELSE 0 END) AS JournalCashAccountNet
FROM dbo.FinancialTransactions ft
LEFT JOIN dbo.JournalEntries je ON je.ReferenceNumber = ft.ReferenceNumber
LEFT JOIN dbo.AccountingEvents ae ON ae.AccountingEventId = je.AccountingEventId
LEFT JOIN dbo.JournalEntryLines jel ON jel.JournalEntryId = je.JournalEntryId
LEFT JOIN dbo.LedgerAccounts la ON la.LedgerAccountId = jel.LedgerAccountId
LEFT JOIN dbo.CashMovements cm ON cm.AccountingEventId = ae.AccountingEventId
GROUP BY ft.FinancialTransactionId, ft.ReferenceNumber, ft.TransactionType, ft.Amount,
         ae.AccountingEventId, ae.PostingAmount, cm.CashMovementId, cm.CashDirection, cm.Amount
HAVING ABS(COALESCE(ft.Amount, 0) - COALESCE(ae.PostingAmount, 0)) > 0.01
    OR ABS(COALESCE(ft.Amount, 0) - COALESCE(cm.Amount, 0)) > 0.01
    OR ABS(COALESCE(ae.PostingAmount, 0) - COALESCE(cm.Amount, 0)) > 0.01
ORDER BY ft.ReferenceNumber, ft.FinancialTransactionId;
'@
    Write-ReadOnlyQueryResult -Marker 'AMOUNT_MISMATCHES' -Sql $amountMismatchesSql

    $payrollPaidSql = @'
SELECT pr.PayrollRecordId,
       pr.NetAmount,
       pr.Status,
       ft.FinancialTransactionId,
       ft.ReferenceNumber AS FinancialTransactionReference,
       ft.Amount AS FinancialTransactionAmount,
       ae.AccountingEventId,
       je.JournalEntryId,
       cm.CashMovementId
FROM dbo.PayrollRecords pr
LEFT JOIN dbo.FinancialTransactions ft ON ft.ReferenceNumber = CAST(pr.PayrollRecordId AS nvarchar(50))
LEFT JOIN dbo.JournalEntries je ON je.ReferenceNumber = ft.ReferenceNumber
LEFT JOIN dbo.AccountingEvents ae ON ae.AccountingEventId = je.AccountingEventId
LEFT JOIN dbo.CashMovements cm ON cm.AccountingEventId = ae.AccountingEventId
WHERE pr.Status = N'Paid'
ORDER BY pr.PayrollRecordId;

SELECT COUNT(*) AS PaidPayrollRecordCount,
       SUM(CASE WHEN pr.Status = N'Paid' THEN 1 ELSE 0 END) AS PaidCount,
       SUM(CASE WHEN pr.Status = N'Paid' AND ft.FinancialTransactionId IS NULL THEN 1 ELSE 0 END) AS PaidWithoutFinancialTransaction,
       SUM(CASE WHEN pr.Status = N'Paid' AND je.JournalEntryId IS NULL THEN 1 ELSE 0 END) AS PaidWithoutJournalEntry,
       SUM(CASE WHEN pr.Status = N'Paid' AND cm.CashMovementId IS NULL THEN 1 ELSE 0 END) AS PaidWithoutCashMovement
FROM dbo.PayrollRecords pr
LEFT JOIN dbo.FinancialTransactions ft ON ft.ReferenceNumber = CAST(pr.PayrollRecordId AS nvarchar(50))
LEFT JOIN dbo.JournalEntries je ON je.ReferenceNumber = ft.ReferenceNumber
LEFT JOIN dbo.CashMovements cm ON cm.AccountingEventId = je.AccountingEventId
WHERE pr.Status = N'Paid';
'@
    Write-ReadOnlyQueryResult -Marker 'PAYROLL_PAID_AUDIT' -Sql $payrollPaidSql

    Write-Host ""
    Write-Host '=== FINAL_READ_ONLY_SUMMARY ==='
    Write-Host 'This script is intentionally read-only. It never executes INSERT, UPDATE, DELETE, MERGE, TRUNCATE, DROP, ALTER, CREATE, migration steps, or any write operation.'
    Write-Host 'The DB guard is active and the audit exits immediately when DB_NAME() is not LUMAR_ERP_ES_VALIDATION.'
}
finally {
    if ($null -ne $script:SqlConnection -and $script:SqlConnection.State -eq [System.Data.ConnectionState]::Open) {
        $script:SqlConnection.Close()
    }
}
