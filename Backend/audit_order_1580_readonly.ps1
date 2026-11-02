#requires -Version 5.1

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:OrderId = 1580
$script:OrderNumber = 'ORD-000027'
$serverName = 'YASIN-YASIN\SQLEXPRESS'
$databaseName = 'LUMAR_ERP_ES_VALIDATION'

Add-Type -AssemblyName System.Data

$connectionStringBuilder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$connectionStringBuilder['Data Source'] = $serverName
$connectionStringBuilder['Initial Catalog'] = $databaseName
$connectionStringBuilder['Integrated Security'] = $true
$connectionStringBuilder['TrustServerCertificate'] = $true
$connectionStringBuilder['Application Name'] = 'LUMAR_Order1580_ReadOnly_Audit'
$connectionStringBuilder['Connect Timeout'] = 15

$connection = New-Object System.Data.SqlClient.SqlConnection($connectionStringBuilder.ConnectionString)

function Invoke-ReadOnlyQuery {
    param([Parameter(Mandatory = $true)][string]$Sql)

    $command = $script:connection.CreateCommand()
    $command.CommandText = $Sql
    $command.CommandTimeout = 45

    $orderIdParameter = $command.Parameters.Add('@OrderId', [System.Data.SqlDbType]::Int)
    $orderIdParameter.Value = $script:OrderId
    $orderNumberParameter = $command.Parameters.Add('@OrderNumber', [System.Data.SqlDbType]::NVarChar, 50)
    $orderNumberParameter.Value = $script:OrderNumber

    $table = New-Object System.Data.DataTable
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($command)
    try {
        [void]$adapter.Fill($table)
    }
    finally {
        $adapter.Dispose()
        $command.Dispose()
    }

    return ,$table
}

function Convert-DataTableToObjects {
    param([Parameter(Mandatory = $true)][System.Data.DataTable]$Table)

    $items = New-Object System.Collections.Generic.List[object]
    foreach ($row in $Table.Rows) {
        $properties = [ordered]@{}
        foreach ($column in $Table.Columns) {
            $value = $row[$column.ColumnName]
            if ($value -is [System.DBNull]) {
                $value = $null
            }
            $properties[$column.ColumnName] = $value
        }
        $items.Add([pscustomobject]$properties)
    }

    return $items.ToArray()
}

function Write-ObjectTable {
    param(
        [Parameter(Mandatory = $true)][string]$Title,
        [AllowNull()][object[]]$Rows
    )

    Write-Host "`n$Title"
    if ($null -eq $Rows -or $Rows.Count -eq 0) {
        Write-Host '(none)'
        return
    }

    $rendered = $Rows | Format-Table -AutoSize -Wrap | Out-String -Width 260
    Write-Host $rendered.TrimEnd()
}

function Get-ValueOrNull {
    param(
        [Parameter(Mandatory = $true)][System.Data.DataRow]$Row,
        [Parameter(Mandatory = $true)][string]$Column
    )

    $value = $Row[$Column]
    if ($value -is [System.DBNull]) {
        return $null
    }
    return $value
}

function Format-Money {
    param([AllowNull()][object]$Value)

    if ($null -eq $Value) {
        return 'NOT STORED'
    }
    return ([decimal]$Value).ToString('N2', [System.Globalization.CultureInfo]::InvariantCulture)
}

$eventScopePredicate = @'
ae.OrderId = @OrderId
OR ae.PaymentId IN (SELECT p.PaymentID FROM dbo.Payments p WHERE p.OrderID = @OrderId)
OR ae.CustomerAdvanceApplicationId IN
   (SELECT app.AdvanceApplicationId FROM dbo.CustomerAdvanceApplications app WHERE app.OrderId = @OrderId)
OR ae.FabricConsumptionSourceId IN
   (SELECT src.FabricConsumptionSourceId
    FROM dbo.FabricConsumptionSources src
    JOIN dbo.OrderItems oi ON oi.OrderItemID = src.OrderItemId
    WHERE oi.OrderID = @OrderId)
OR ae.AccountingEventId IN
   (SELECT reversal.AccountingEventId
    FROM dbo.FabricConsumptionPartialReversals reversal
    JOIN dbo.FabricConsumptionSources src ON src.FabricConsumptionSourceId = reversal.FabricConsumptionSourceId
    JOIN dbo.OrderItems oi ON oi.OrderItemID = src.OrderItemId
    WHERE oi.OrderID = @OrderId)
'@

try {
    $connection.Open()

    $headerTable = Invoke-ReadOnlyQuery @'
SELECT OrderID, OrderNumber, CustomerID, CreatedDate, UpdatedDate,
       TotalAmount, DiscountAmount, PaidAmount, RemainingAmount, OrderStatus
FROM dbo.Orders
WHERE OrderID = @OrderId;
'@
    if ($headerTable.Rows.Count -ne 1 -or [string]$headerTable.Rows[0]['OrderNumber'] -ne $script:OrderNumber) {
        throw 'The requested OrderID and OrderNumber did not resolve to exactly one matching order.'
    }
    $header = Convert-DataTableToObjects $headerTable
    Write-ObjectTable 'ORDER_HEADER' $header

    $piecesTable = Invoke-ReadOnlyQuery @'
SELECT p.PieceID, p.OrderItemID, p.PieceNumber, p.PieceStatus, p.TrackingCode,
       oi.ProductTypeId, pt.NameAr AS OfficialProductTypeName,
       CASE WHEN p.TrackingCode = N'TRK-000064' THEN N'ORIGINAL PIECE'
            WHEN p.TrackingCode = N'TRK-000066' THEN N'ADDED PIECE'
            ELSE N'' END AS RequestedPieceRole
FROM dbo.Pieces p
JOIN dbo.OrderItems oi ON oi.OrderItemID = p.OrderItemID
LEFT JOIN dbo.PricingProductTypes pt ON pt.ProductTypeId = oi.ProductTypeId
WHERE oi.OrderID = @OrderId
ORDER BY p.CreatedDate, p.PieceID;
'@
    $pieceRows = Convert-DataTableToObjects $piecesTable
    Write-ObjectTable 'PIECES' $pieceRows

    $pricesTable = Invoke-ReadOnlyQuery @'
SELECT p.PieceID, p.TrackingCode, p.PieceStatus,
       value.OriginalSalePrice, value.ApprovedSalePrice,
       value.OriginalDiscountShare, value.DiscountShare,
       value.OriginalBasePoints, value.BenefitsCaptured,
       value.CreatedAt, value.UpdatedAt
FROM dbo.Pieces p
JOIN dbo.OrderItems oi ON oi.OrderItemID = p.OrderItemID
LEFT JOIN dbo.TailoringPieceSaleValues value ON value.PieceID = p.PieceID
WHERE oi.OrderID = @OrderId
ORDER BY p.CreatedDate, p.PieceID;
'@
    $priceRows = Convert-DataTableToObjects $pricesTable
    Write-ObjectTable 'PIECE_PRICES' $priceRows

    $fabricDetailsTable = Invoke-ReadOnlyQuery @'
SELECT p.PieceID, p.TrackingCode, oi.OrderItemID,
       f.OrderItemFabricID, f.FabricCode, f.FabricType, f.FabricColor,
       f.Quantity AS RequiredQuantity, f.ConsumedQuantity, f.Unit,
       f.InventoryItemID, foundation.OfficialUnitCost,
       inventory.YardPrice AS OrderFabricDtoYardPrice,
       f.UnitCost AS OrderItemFabricUnitCost,
       f.TotalCost AS OrderItemFabricTotalCost,
       CAST(f.ConsumedQuantity * COALESCE(inventory.YardPrice, 0) AS decimal(18,2)) AS FabricValueFromOrderFabricDtoYardPrice
FROM dbo.OrderItems oi
JOIN dbo.Pieces p ON p.OrderItemID = oi.OrderItemID
LEFT JOIN dbo.OrderItemFabrics f ON f.OrderItemID = oi.OrderItemID
LEFT JOIN dbo.InventoryItemFoundation foundation ON foundation.InventoryItemId = f.InventoryItemID
LEFT JOIN dbo.InventoryItems inventory ON inventory.InventoryItemID = f.InventoryItemID
WHERE oi.OrderID = @OrderId
ORDER BY p.CreatedDate, p.PieceID, f.OrderItemFabricID;
'@
    $fabricDetailsRows = Convert-DataTableToObjects $fabricDetailsTable
    Write-ObjectTable 'FABRIC_DETAILS' $fabricDetailsRows

    $fabricConsumptionTable = Invoke-ReadOnlyQuery @'
SELECT p.PieceID, p.TrackingCode, oi.OrderItemID,
       src.FabricConsumptionSourceId, src.SourceOperationId,
       src.InventoryItemId, src.ConsumedQuantity, src.UnitId,
       src.OfficialUnitCost, src.OperationalAmount, src.PostingAmount,
       src.InventoryTransactionId, src.AccountingEventId, src.CreatedAt
FROM dbo.FabricConsumptionSources src
JOIN dbo.OrderItems oi ON oi.OrderItemID = src.OrderItemId
JOIN dbo.Pieces p ON p.OrderItemID = oi.OrderItemID
                 AND (src.PieceId IS NULL OR src.PieceId = p.PieceID)
WHERE oi.OrderID = @OrderId
ORDER BY src.CreatedAt, src.FabricConsumptionSourceId;
'@
    $fabricConsumptionRows = Convert-DataTableToObjects $fabricConsumptionTable
    Write-ObjectTable 'FABRIC_CONSUMPTION' $fabricConsumptionRows

    $editOperationsTable = Invoke-ReadOnlyQuery @'
SELECT EditOperationId, CreatedAt, ResultJson
FROM dbo.TailoringOrderEditOperations
WHERE OrderId = @OrderId
ORDER BY CreatedAt, EditOperationId;
'@
    $editOperationRows = New-Object System.Collections.Generic.List[object]
    foreach ($row in $editOperationsTable.Rows) {
        $resultJson = Get-ValueOrNull $row 'ResultJson'
        $snapshot = $null
        if (-not [string]::IsNullOrWhiteSpace([string]$resultJson)) {
            try {
                $snapshot = ConvertFrom-Json -InputObject ([string]$resultJson)
            }
            catch {
                $snapshot = $null
            }
        }

        $operationOrder = if ($null -ne $snapshot) { $snapshot.Order } else { $null }
        $operationPieces = if ($null -ne $snapshot) { @($snapshot.Pieces) } else { @() }
        $statuses = @($operationPieces | ForEach-Object {
            '{0}:{1}' -f $_.TrackingCode, $_.PieceStatus
        }) -join '; '
        $editOperationRows.Add([pscustomobject]@{
            EditOperationId = $row['EditOperationId']
            CreatedAt = $row['CreatedAt']
            SnapshotAvailable = ($null -ne $operationOrder)
            TotalAmount = if ($null -ne $operationOrder) { $operationOrder.TotalAmount } else { $null }
            DiscountAmount = if ($null -ne $operationOrder) { $operationOrder.DiscountAmount } else { $null }
            PaidAmount = if ($null -ne $operationOrder) { $operationOrder.PaidAmount } else { $null }
            RemainingAmount = if ($null -ne $operationOrder) { $operationOrder.RemainingAmount } else { $null }
            PieceCount = $operationPieces.Count
            PieceStatuses = $statuses
        })
    }
    $editRows = $editOperationRows.ToArray()

    $addOperationsTable = Invoke-ReadOnlyQuery @'
SELECT addOp.AddOperationId, addOp.EditOperationId, addOp.OrderId,
       addOp.PieceId, p.TrackingCode, editOp.CreatedAt AS EditCreatedAt
FROM dbo.TailoringPieceAddOperations addOp
LEFT JOIN dbo.Pieces p ON p.PieceID = addOp.PieceId
LEFT JOIN dbo.TailoringOrderEditOperations editOp
       ON editOp.EditOperationId = addOp.EditOperationId AND editOp.OrderId = addOp.OrderId
WHERE addOp.OrderId = @OrderId
ORDER BY editOp.CreatedAt, addOp.AddOperationId;
'@
    $addOperationRows = Convert-DataTableToObjects $addOperationsTable

    $cancellationsTable = Invoke-ReadOnlyQuery @'
SELECT cancellation.PieceId, cancellation.EditOperationId, cancellation.Reason,
       cancellation.CancelledAt,
        COUNT(*) OVER (PARTITION BY cancellation.PieceId) AS CancellationCountForPiece,
        CASE WHEN piece.PieceID IS NOT NULL THEN N'YES' ELSE N'NO' END AS CancelledPieceStillExists,
        CASE WHEN piece.PieceID IS NOT NULL THEN N'NO' ELSE N'YES' END AS DeleteOccurred
FROM dbo.TailoringPieceCancellations cancellation
    JOIN dbo.TailoringOrderEditOperations editOp
        ON editOp.EditOperationId = cancellation.EditOperationId AND editOp.OrderId = @OrderId
    LEFT JOIN dbo.Pieces piece ON piece.PieceID = cancellation.PieceId
ORDER BY cancellation.CancelledAt, cancellation.EditOperationId, cancellation.PieceId;
'@
    $cancellationRows = Convert-DataTableToObjects $cancellationsTable

    $classifiedEditRows = foreach ($edit in $editRows) {
        $kind = 'EDIT'
        if (@($addOperationRows | Where-Object { $_.EditOperationId -eq $edit.EditOperationId }).Count -gt 0) {
            $kind = 'ADD'
        }
        if (@($cancellationRows | Where-Object { $_.EditOperationId -eq $edit.EditOperationId }).Count -gt 0) {
            $kind = 'CANCEL'
        }
        [pscustomobject]@{
            OperationKind = $kind
            EditOperationId = $edit.EditOperationId
            CreatedAt = $edit.CreatedAt
            SnapshotAvailable = $edit.SnapshotAvailable
            TotalAmount = $edit.TotalAmount
            DiscountAmount = $edit.DiscountAmount
            PaidAmount = $edit.PaidAmount
            RemainingAmount = $edit.RemainingAmount
            PieceCount = $edit.PieceCount
            PieceStatuses = $edit.PieceStatuses
        }
    }
    Write-ObjectTable 'EDIT_OPERATIONS' @($classifiedEditRows)
    Write-ObjectTable 'ADD_OPERATIONS' $addOperationRows
    Write-ObjectTable 'CANCELLATIONS' $cancellationRows

    $paymentsTable = Invoke-ReadOnlyQuery @'
SELECT PaymentID, OrderID, PaymentDate, Amount, PaymentMethod,
       ReferenceNo, PaymentKind, CreatedDate
FROM dbo.Payments
WHERE OrderID = @OrderId
ORDER BY PaymentDate, PaymentID;
'@
    $paymentRows = Convert-DataTableToObjects $paymentsTable
    Write-ObjectTable 'PAYMENTS' $paymentRows

    $advancesTable = Invoke-ReadOnlyQuery @'
SELECT payment.PaymentID AS AdvancePaymentId, payment.Amount AS AdvanceAmount,
       payment.ReferenceNo, payment.PaymentDate, payment.CreatedDate AS PaymentCreatedAt,
       app.AdvanceApplicationId, app.AppliedAmount, app.CreatedAt AS ApplicationCreatedAt
FROM dbo.Payments payment
LEFT JOIN dbo.CustomerAdvanceApplications app ON app.AdvancePaymentId = payment.PaymentID
WHERE payment.OrderID = @OrderId AND payment.PaymentKind = N'Advance'
ORDER BY payment.PaymentDate, payment.PaymentID, app.AdvanceApplicationId;
'@
    $advanceRows = Convert-DataTableToObjects $advancesTable
    Write-ObjectTable 'CUSTOMER_ADVANCES' $advanceRows

    $refundsTable = Invoke-ReadOnlyQuery @'
SELECT payment.PaymentID, payment.OrderID, payment.PaymentDate, payment.Amount,
       payment.ReferenceNo, payment.PaymentKind, payment.CreatedDate,
       event.AccountingEventId, event.AccountingEventType, event.SourceOperationId,
       event.SourceType, event.SourceId, event.CreatedAt AS AccountingEventCreatedAt
FROM dbo.Payments payment
LEFT JOIN dbo.AccountingEvents event ON event.PaymentId = payment.PaymentID
WHERE payment.OrderID = @OrderId AND payment.PaymentKind = N'Refund'
ORDER BY payment.PaymentDate, payment.PaymentID, event.AccountingEventId;
'@
    $refundRows = Convert-DataTableToObjects $refundsTable
    Write-ObjectTable 'REFUNDS' $refundRows

    $accountingEventsSql = @"
SELECT ae.AccountingEventId, ae.AccountingEventType, ae.PostingAmount,
       ae.PaymentId, ae.OrderId, ae.CustomerAdvanceApplicationId,
       ae.FabricConsumptionSourceId, ae.SourceType, ae.SourceId,
       ae.SourceOperationId, ae.OriginalAccountingEventId,
       ae.ReversalReason, ae.Status, ae.CreatedAt
FROM dbo.AccountingEvents ae
WHERE (
$eventScopePredicate
)
ORDER BY ae.CreatedAt, ae.AccountingEventId;
"@
    $accountingEventsTable = Invoke-ReadOnlyQuery $accountingEventsSql
    $accountingEventRows = Convert-DataTableToObjects $accountingEventsTable
    Write-ObjectTable 'ACCOUNTING_EVENTS' $accountingEventRows

    $financialTransactionsSql = @"
SELECT transactionRow.FinancialTransactionId, transactionRow.ReferenceNumber,
       transactionRow.TransactionType, transactionRow.Amount,
       transactionRow.AccountingEventId, transactionRow.CreatedAt
FROM dbo.FinancialTransactions transactionRow
WHERE transactionRow.AccountingEventId IN (
    SELECT ae.AccountingEventId FROM dbo.AccountingEvents ae WHERE (
$eventScopePredicate
    )
)
OR transactionRow.ReferenceNumber LIKE @OrderNumber + N'%'
ORDER BY transactionRow.CreatedAt, transactionRow.FinancialTransactionId;
"@
    $financialTransactionsTable = Invoke-ReadOnlyQuery $financialTransactionsSql
    $financialTransactionRows = Convert-DataTableToObjects $financialTransactionsTable
    Write-ObjectTable 'FINANCIAL_TRANSACTIONS' $financialTransactionRows

    $customerLedgerTable = Invoke-ReadOnlyQuery @'
SELECT CustomerLedgerEntryId, CustomerID, ReferenceNumber,
       DebitAmount, CreditAmount, BalanceAfterTransaction, CreatedAt
FROM dbo.CustomerLedgerEntries
WHERE ReferenceNumber LIKE @OrderNumber + N'%'
ORDER BY CreatedAt, CustomerLedgerEntryId;
'@
    $customerLedgerRows = Convert-DataTableToObjects $customerLedgerTable
    Write-ObjectTable 'CUSTOMER_LEDGER' $customerLedgerRows

    $cashMovementsSql = @"
SELECT movement.CashMovementId, movement.AccountingEventId,
       movement.CashDirection, movement.Amount, movement.OccurredAt,
       movement.CreatedAt, movement.ReferenceNumber, movement.SourceType,
       movement.SourceId, movement.SourceOperationId, movement.OriginalCashMovementId,
       movement.Status
FROM dbo.CashMovements movement
WHERE movement.AccountingEventId IN (
    SELECT ae.AccountingEventId FROM dbo.AccountingEvents ae WHERE (
$eventScopePredicate
    )
)
OR movement.ReferenceNumber LIKE @OrderNumber + N'%'
ORDER BY movement.OccurredAt, movement.CashMovementId;
"@
    $cashMovementsTable = Invoke-ReadOnlyQuery $cashMovementsSql
    $cashMovementRows = Convert-DataTableToObjects $cashMovementsTable
    Write-ObjectTable 'CASH_MOVEMENTS' $cashMovementRows

    $journalsSql = @"
SELECT journal.JournalEntryId, journal.ReferenceNumber, journal.Description,
       journal.EntryDate, journal.CreatedAt, journal.AccountingEventId,
       line.JournalEntryLineId, line.LedgerAccountId,
       line.DebitAmount, line.CreditAmount, line.Description AS LineDescription
FROM dbo.JournalEntries journal
LEFT JOIN dbo.JournalEntryLines line ON line.JournalEntryId = journal.JournalEntryId
WHERE journal.AccountingEventId IN (
    SELECT ae.AccountingEventId FROM dbo.AccountingEvents ae WHERE (
$eventScopePredicate
    )
)
OR journal.ReferenceNumber LIKE @OrderNumber + N'%'
ORDER BY journal.EntryDate, journal.JournalEntryId, line.JournalEntryLineId;
"@
    $journalsTable = Invoke-ReadOnlyQuery $journalsSql
    $journalRows = Convert-DataTableToObjects $journalsTable
    Write-ObjectTable 'JOURNALS' $journalRows

    $loyaltyEffectsTable = Invoke-ReadOnlyQuery @'
SELECT loyalty.LoyaltyTransactionId AS EffectId, loyalty.TransactionType,
       loyalty.Points AS Amount, loyalty.Source, loyalty.Notes, loyalty.CreatedAt,
       balance.BenefitKind, balance.ReferralLevel, balance.OriginalPoints,
       balance.CurrentPoints, balance.OriginalLoyaltyTransactionId,
       balance.OriginalReferralTransactionId
FROM dbo.LoyaltyTransactions loyalty
LEFT JOIN dbo.TailoringPieceBenefitBalances balance
       ON balance.OriginalLoyaltyTransactionId = loyalty.LoyaltyTransactionId
WHERE loyalty.OrderId = @OrderId
  AND EXISTS (
      SELECT 1
      FROM dbo.TailoringPieceCancellations cancellation
     JOIN dbo.TailoringOrderEditOperations editOp
         ON editOp.EditOperationId = cancellation.EditOperationId AND editOp.OrderId = @OrderId
     WHERE editOp.OrderId = @OrderId
        AND (balance.PieceId = cancellation.PieceId
             OR loyalty.Notes LIKE N'%|PieceId:' + CONVERT(nvarchar(20), cancellation.PieceId) + N'|%')
  )
ORDER BY loyalty.CreatedAt, loyalty.LoyaltyTransactionId;
'@
    $loyaltyEffectRows = Convert-DataTableToObjects $loyaltyEffectsTable
    Write-ObjectTable 'LOYALTY_EFFECTS' $loyaltyEffectRows

    $referralEffectsTable = Invoke-ReadOnlyQuery @'
SELECT referral.ReferralTransactionId AS EffectId, referral.TransactionType,
       referral.FixedRewardAmount, referral.LoyaltyPoints AS Amount,
       referral.Notes, referral.CreatedAt, referral.ReferrerCustomerId,
       referral.ReferredCustomerId, referral.ReferralRewardId
FROM dbo.ReferralTransactions referral
WHERE referral.OrderId = @OrderId
  AND EXISTS (
      SELECT 1
      FROM dbo.TailoringPieceCancellations cancellation
            JOIN dbo.TailoringOrderEditOperations editOp
                     ON editOp.EditOperationId = cancellation.EditOperationId AND editOp.OrderId = @OrderId
            WHERE editOp.OrderId = @OrderId
        AND referral.Notes LIKE N'%|PieceId:' + CONVERT(nvarchar(20), cancellation.PieceId) + N'|%'
  )
ORDER BY referral.CreatedAt, referral.ReferralTransactionId;
'@
    $referralEffectRows = Convert-DataTableToObjects $referralEffectsTable
    Write-ObjectTable 'REFERRAL_EFFECTS' $referralEffectRows

    $trackingTable = Invoke-ReadOnlyQuery @'
SELECT event.TrackingEventID, event.PieceID, event.OrderItemID,
       event.TrackingCode, event.Stage, event.Status, event.EventTime,
       event.IsReverted, event.RevertedAt
FROM dbo.TrackingEvents event
WHERE event.OrderID = @OrderId
  AND event.PieceID IN (
      SELECT cancellation.PieceId
      FROM dbo.TailoringPieceCancellations cancellation
      JOIN dbo.TailoringOrderEditOperations editOp
           ON editOp.EditOperationId = cancellation.EditOperationId AND editOp.OrderId = @OrderId
  )
ORDER BY event.EventTime, event.TrackingEventID;
'@
    $trackingRows = Convert-DataTableToObjects $trackingTable
    Write-ObjectTable 'TRACKING_EVENTS' $trackingRows

    $pieceWagesTable = Invoke-ReadOnlyQuery @'
SELECT wage.PieceWageRecordID, wage.OrderID, wage.OrderItemID, wage.PieceID,
       wage.TrackingEventID, wage.Stage, wage.Quantity, wage.WageRate,
       wage.TotalWage, wage.Status, wage.CreatedAt
FROM dbo.PieceWageRecords wage
WHERE wage.OrderID = @OrderId
  AND wage.PieceID IN (
      SELECT cancellation.PieceId
      FROM dbo.TailoringPieceCancellations cancellation
      JOIN dbo.TailoringOrderEditOperations editOp
           ON editOp.EditOperationId = cancellation.EditOperationId AND editOp.OrderId = @OrderId
  )
ORDER BY wage.CreatedAt, wage.PieceWageRecordID;
'@
    $pieceWageRows = Convert-DataTableToObjects $pieceWagesTable
    Write-ObjectTable 'PIECE_WAGES' $pieceWageRows

    $partialReversalsTable = Invoke-ReadOnlyQuery @'
SELECT reversal.FabricConsumptionPartialReversalId,
       reversal.FabricConsumptionSourceId, reversal.PieceId,
       reversal.SourceOperationId, reversal.ReturnedQuantity,
       reversal.OperationalAmount, reversal.PostingAmount,
       reversal.AccountingEventId, reversal.InventoryTransactionId,
       reversal.Reason, reversal.CreatedAt
FROM dbo.FabricConsumptionPartialReversals reversal
JOIN dbo.FabricConsumptionSources sourceRow
       ON sourceRow.FabricConsumptionSourceId = reversal.FabricConsumptionSourceId
JOIN dbo.OrderItems oi ON oi.OrderItemID = sourceRow.OrderItemId
WHERE oi.OrderID = @OrderId
  AND reversal.PieceId IN (
      SELECT cancellation.PieceId
      FROM dbo.TailoringPieceCancellations cancellation
      JOIN dbo.TailoringOrderEditOperations editOp
           ON editOp.EditOperationId = cancellation.EditOperationId AND editOp.OrderId = @OrderId
  )
ORDER BY reversal.CreatedAt, reversal.FabricConsumptionPartialReversalId;
'@
    $partialReversalRows = Convert-DataTableToObjects $partialReversalsTable
    Write-ObjectTable 'PARTIAL_REVERSALS' $partialReversalRows

    $duplicateCheckTable = Invoke-ReadOnlyQuery @'
SELECT checkRow.CheckName, checkRow.Result
FROM (
    SELECT N'Duplicate EditOperationId' AS CheckName,
           CASE WHEN EXISTS (
               SELECT EditOperationId FROM dbo.TailoringOrderEditOperations
               WHERE OrderId = @OrderId GROUP BY EditOperationId HAVING COUNT(*) > 1
           ) THEN N'YES' ELSE N'NO' END AS Result
    UNION ALL
    SELECT N'Duplicate AddOperationId',
           CASE WHEN EXISTS (
               SELECT AddOperationId FROM dbo.TailoringPieceAddOperations
               WHERE OrderId = @OrderId GROUP BY AddOperationId HAVING COUNT(*) > 1
           ) THEN N'YES' ELSE N'NO' END
    UNION ALL
    SELECT N'Duplicate PieceCancellation',
           CASE WHEN EXISTS (
               SELECT cancellation.PieceId
               FROM dbo.TailoringPieceCancellations cancellation
               JOIN dbo.TailoringOrderEditOperations editOp
                    ON editOp.EditOperationId = cancellation.EditOperationId AND editOp.OrderId = @OrderId
               GROUP BY cancellation.PieceId HAVING COUNT(*) > 1
           ) THEN N'YES' ELSE N'NO' END
    UNION ALL
    SELECT N'Duplicate PartialReversal Operation',
           CASE WHEN EXISTS (
               SELECT reversal.SourceOperationId
               FROM dbo.FabricConsumptionPartialReversals reversal
               JOIN dbo.FabricConsumptionSources sourceRow
                    ON sourceRow.FabricConsumptionSourceId = reversal.FabricConsumptionSourceId
               JOIN dbo.OrderItems oi ON oi.OrderItemID = sourceRow.OrderItemId
               WHERE oi.OrderID = @OrderId
               GROUP BY reversal.SourceOperationId HAVING COUNT(*) > 1
           ) THEN N'YES' ELSE N'NO' END
    UNION ALL
    SELECT N'Duplicate Payment Operation (OrderId, ReferenceNo, PaymentKind)',
           CASE WHEN EXISTS (
               SELECT ReferenceNo, PaymentKind FROM dbo.Payments
               WHERE OrderID = @OrderId AND PaymentKind <> N'Refund' AND ReferenceNo IS NOT NULL
               GROUP BY ReferenceNo, PaymentKind HAVING COUNT(*) > 1
           ) THEN N'YES' ELSE N'NO' END
    UNION ALL
    SELECT N'Duplicate Refund Operation (OrderId, ReferenceNo, PaymentKind)',
           CASE WHEN EXISTS (
               SELECT ReferenceNo, PaymentKind FROM dbo.Payments
               WHERE OrderID = @OrderId AND PaymentKind = N'Refund' AND ReferenceNo IS NOT NULL
               GROUP BY ReferenceNo, PaymentKind HAVING COUNT(*) > 1
           ) THEN N'YES' ELSE N'NO' END
) AS checkRow
ORDER BY checkRow.CheckName;
'@
    $duplicateCheckRows = Convert-DataTableToObjects $duplicateCheckTable
    Write-ObjectTable 'DUPLICATE_CHECK' $duplicateCheckRows

    Write-Host "`nOPERATION_TIMELINE"
    Write-Host 'Operation snapshots are persisted after-states only. Missing earlier totals are reported as NOT STORED; no historical amounts are reconstructed.'
    $firstAdd = $addOperationRows | Sort-Object EditCreatedAt | Select-Object -First 1
    if ($null -ne $firstAdd) {
        $beforeAdd = $editRows |
            Where-Object { $_.CreatedAt -lt $firstAdd.EditCreatedAt } |
            Sort-Object CreatedAt, EditOperationId |
            Select-Object -Last 1
        $afterAdd = $editRows | Where-Object { $_.EditOperationId -eq $firstAdd.EditOperationId } | Select-Object -First 1
        Write-Host ('BEFORE ADD: {0}' -f $(if ($null -ne $beforeAdd) { Format-Money $beforeAdd.TotalAmount } else { 'NOT STORED' }))
        Write-Host ('AFTER ADD: {0}' -f $(if ($null -ne $afterAdd -and $afterAdd.SnapshotAvailable) { Format-Money $afterAdd.TotalAmount } else { 'NOT STORED' }))
    }
    else {
        Write-Host 'BEFORE ADD: NOT STORED'
        Write-Host 'AFTER ADD: NOT STORED'
    }

    $firstCancellation = $cancellationRows | Sort-Object CancelledAt | Select-Object -First 1
    $totalBeforeCancellation = $null
    $afterCancellation = $null
    if ($null -ne $firstCancellation) {
        $cancelEdit = $editRows | Where-Object { $_.EditOperationId -eq $firstCancellation.EditOperationId } | Select-Object -First 1
        if ($null -ne $cancelEdit) {
            $totalBeforeCancellation = $editRows |
                Where-Object { $_.CreatedAt -lt $cancelEdit.CreatedAt } |
                Sort-Object CreatedAt, EditOperationId |
                Select-Object -Last 1
            $afterCancellation = $cancelEdit
        }
    }
    Write-Host ('ORDER TOTAL BEFORE CANCELLATION: {0}' -f $(if ($null -ne $totalBeforeCancellation) { Format-Money $totalBeforeCancellation.TotalAmount } else { 'NOT STORED' }))
    Write-Host ('ORDER TOTAL AFTER CANCELLATION: {0}' -f $(if ($null -ne $afterCancellation -and $afterCancellation.SnapshotAvailable) { Format-Money $afterCancellation.TotalAmount } else { 'NOT STORED' }))

    $paymentTotalsTable = Invoke-ReadOnlyQuery @'
SELECT COALESCE(SUM(CASE WHEN PaymentKind = N'Refund' THEN 0 ELSE Amount END), 0) AS PaymentTotal,
       COALESCE(SUM(CASE WHEN PaymentKind = N'Refund' THEN Amount ELSE 0 END), 0) AS RefundTotal
FROM dbo.Payments
WHERE OrderID = @OrderId;
'@
    $paymentTotals = $paymentTotalsTable.Rows[0]
    $paymentTotal = [decimal]$paymentTotals['PaymentTotal']
    $refundTotal = [decimal]$paymentTotals['RefundTotal']
    $netPaymentTotal = $paymentTotal - $refundTotal
    $currentPaid = [decimal]$headerTable.Rows[0]['PaidAmount']
    $unexplainedPaidDifference = $currentPaid - $netPaymentTotal

    $cancelledPieceIds = @($cancellationRows | Select-Object -ExpandProperty PieceId -Unique)
    $cancelledPieceValues = @($priceRows | Where-Object { $cancelledPieceIds -contains $_.PieceID })
    $cancelledValueText = if ($cancelledPieceValues.Count -eq 0) {
        'NOT STORED'
    }
    else {
        ($cancelledPieceValues | ForEach-Object { '{0}={1}' -f $_.PieceID, (Format-Money $_.OriginalSalePrice) }) -join '; '
    }

    $originalFabricDetails = @($fabricDetailsRows | Where-Object { $_.TrackingCode -eq 'TRK-000064' })
    $originalFabricSources = @($fabricConsumptionRows | Where-Object { $_.TrackingCode -eq 'TRK-000064' })
    $originalOperationalCost = if ($originalFabricSources.Count -gt 0) {
        ($originalFabricSources | Measure-Object -Property OperationalAmount -Sum).Sum
    }
    else {
        $null
    }
    $originalPieceTotal = if ($originalFabricDetails.Count -gt 0) {
        ($originalFabricDetails | Measure-Object -Property OrderItemFabricTotalCost -Sum).Sum
    }
    else {
        $null
    }

    Write-Host "`nFINAL_SUMMARY"
    Write-Host ('CURRENT_TOTAL: {0}' -f (Format-Money $headerTable.Rows[0]['TotalAmount']))
    Write-Host ('CURRENT_DISCOUNT: {0}' -f (Format-Money $headerTable.Rows[0]['DiscountAmount']))
    Write-Host ('CURRENT_PAID: {0}' -f (Format-Money $headerTable.Rows[0]['PaidAmount']))
    Write-Host ('CURRENT_REMAINING: {0}' -f (Format-Money $headerTable.Rows[0]['RemainingAmount']))
    Write-Host ('ACTUAL_PAYMENT_TOTAL_FROM_PAYMENT_RECORDS: {0}' -f (Format-Money $paymentTotal))
    Write-Host ('ACTUAL_REFUND_TOTAL: {0}' -f (Format-Money $refundTotal))
    Write-Host ('NET_PAYMENT_RECORDS: {0}' -f (Format-Money $netPaymentTotal))
    Write-Host ('UNEXPLAINED_PAID_DIFFERENCE: {0}' -f (Format-Money $unexplainedPaidDifference))
    Write-Host ('PAID_EQUALS_CURRENT_TOTAL: {0}' -f $(if ($currentPaid -eq [decimal]$headerTable.Rows[0]['TotalAmount']) { 'YES' } else { 'NO' }))
    Write-Host ('CANCELLED_PIECE_ID: {0}' -f $(if ($cancelledPieceIds.Count -gt 0) { $cancelledPieceIds -join ', ' } else { 'NOT FOUND' }))
    Write-Host ('CANCELLED_PIECE_ORIGINAL_VALUE: {0}' -f $cancelledValueText)
    Write-Host ('ORIGINAL_PIECE_OPERATIONAL_COST_DB: {0}' -f (Format-Money $originalOperationalCost))
    Write-Host ('ORIGINAL_PIECE_TOTAL_DB: {0}' -f (Format-Money $originalPieceTotal))
    Write-Host 'READ_ONLY_AUDIT_COMPLETE'
}
catch {
    Write-Error ('Read-only audit stopped: {0}' -f $_.Exception.Message)
    exit 1
}
finally {
    if ($connection.State -ne [System.Data.ConnectionState]::Closed) {
        $connection.Close()
    }
    $connection.Dispose()
}