SET NOCOUNT ON;
DECLARE @n int = (SELECT COUNT(*) FROM dbo.AccountingEvents WHERE AccountingEventType BETWEEN 20 AND 35);
SELECT TOP 50
    AccountingEventId,
    AccountingEventType,
    SourceType,
    SourceId,
    SourceOperationId,
    OriginalAccountingEventId,
    OrderId,
    PaymentId,
    CustomerAdvanceApplicationId,
    InventoryReceiptPostingId,
    FabricConsumptionSourceId,
    ProductionMaterialConsumptionId,
    ReadyMadeSaleCostPostingId,
    ImportedReadyMadeSaleCostPostingId,
    ToolIssuanceId,
    ToolIssuanceReversalId,
    ReversalReason,
    ReversedBy,
    ReversedAt,
    Status
FROM dbo.AccountingEvents
WHERE AccountingEventType BETWEEN 20 AND 35
ORDER BY AccountingEventId;
SELECT 'COUNT=' + CONVERT(varchar(20), @n);
