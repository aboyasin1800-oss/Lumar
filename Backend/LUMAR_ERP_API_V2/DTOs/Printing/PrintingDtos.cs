namespace LUMAR_ERP_API_V2.DTOs.Printing;

public sealed record PrepareMeasurementCardPrintDto(
    Guid? PrintRequestId,
    string? ReprintReasonCode,
    string? DamageReason,
    int? ResponsibleEmployeeId,
    string? Notes,
    decimal? SaleAmount,
    string? SalePaymentType,
    int? SaleCustomerId);

public sealed record FailMeasurementCardPrintDto(string? FailureReason);
public sealed record CompleteMeasurementCardPrintDto(int? CashAccountId);

public sealed record MeasurementCardReservedRecoveryDto(int RecoveredCount);

public sealed record MeasurementCardStatusTypeDto(int ProductTypeId, string ProductTypeName, int Count);

public sealed record MeasurementCardStatusSummaryDto(
    Guid SnapshotId,
    DateTime CapturedAtUtc,
    int PrintedTotal,
    int UnprintedTotal,
    IReadOnlyList<MeasurementCardStatusTypeDto> UnprintedByProductType);

public sealed record EligibleMeasurementCardDto(
    bool IsReadyMade,
    int PieceId,
    int? OrderId,
    int? OrderItemId,
    string OrderNumber,
    int PieceNumber,
    string CustomerName,
    string PieceType,
    int? ProductTypeId,
    string TrackingCode,
    string? Barcode,
    string? MeasurementSnapshot,
    string? FabricCode,
    string? FabricType,
    string? FabricColor,
    string? CatalogNumber,
    string? Request1,
    string? Request2,
    string? Notes,
    string PieceStatus,
    DateTime? DeliveryDate,
    int CopyNumber);

public sealed record MeasurementCardBulkPrepareDto(Guid BatchRequestId, Guid SnapshotId, int? OrderId);

public sealed record MeasurementCardBulkConfirmationDto(Guid BatchRequestId, IReadOnlyList<int> PrintHistoryIds);

public sealed record MeasurementCardBulkConfirmationItemDto(int PrintHistoryId, string PrintStatus);

public sealed record MeasurementCardBulkConfirmationResultDto(
    Guid BatchRequestId,
    IReadOnlyList<MeasurementCardBulkConfirmationItemDto> Confirmed,
    IReadOnlyList<string> Skipped);

public sealed record MeasurementCardBulkReconcileDto(Guid BatchRequestId, IReadOnlyList<int> PrintHistoryIds);

public sealed record MeasurementCardBulkReconcileItemDto(
    int PrintHistoryId,
    string Classification,
    bool BelongsToBatch,
    string PrintStatus);

public sealed record MeasurementCardBulkReconcileResultDto(
    Guid BatchRequestId,
    IReadOnlyList<MeasurementCardBulkReconcileItemDto> Items);

public sealed record MeasurementCardBulkRecoveryDto(Guid BatchRequestId, IReadOnlyList<int> PrintHistoryIds);

public sealed record MeasurementCardBulkPrepareItemDto(
    bool IsReadyMade,
    int PieceId,
    int PrintHistoryId,
    Guid PrintRequestId,
    int CopyNumber,
    string PrintStatus);

public sealed record MeasurementCardEligibleSnapshotDto(
    Guid SnapshotId,
    DateTime CapturedAtUtc,
    IReadOnlyList<EligibleMeasurementCardDto> Cards);

public sealed record MeasurementCardBulkPrepareResultDto(
    Guid BatchRequestId,
    Guid SnapshotId,
    DateTime CapturedAtUtc,
    IReadOnlyList<MeasurementCardBulkPrepareItemDto> ReservedItems,
    IReadOnlyList<EligibleMeasurementCardDto> Cards,
    IReadOnlyList<string> Skipped);

public sealed record MeasurementCardPrintHistoryDto(
    int PrintHistoryId,
    Guid PrintRequestId,
    string PrintStatus,
    int? OrderId,
    int? OrderItemId,
    int? PieceId,
    int? ReadyMadeProductionOrderId,
    int? ReadyMadeProductionOrderItemId,
    int? ReadyMadePieceId,
    string TrackingCode,
    DateTime? PrintedAtUtc,
    int PrintedByUserId,
    string PrintedByDisplayName,
    int CopyNumber,
    string? ReprintReasonCode,
    string? DamageReason,
    int? ResponsibleEmployeeId,
    string? Notes,
    decimal? SaleAmount,
    string? SalePaymentType,
    int? SaleCustomerId,
    string? FinancialTransactionReference,
    int? FinancialTransactionId,
    int? JournalEntryId,
    int? CustomerLedgerEntryId,
    int? PaymentId,
    string? PaymentReferenceNumber,
    int? PaymentFinancialTransactionId,
    int? PaymentJournalEntryId,
    int? PaymentCustomerLedgerEntryId,
    string? WaiverReferenceNumber,
    int? WaiverFinancialTransactionId,
    int? WaiverJournalEntryId,
    int? WaiverCustomerLedgerEntryId,
    DateTime? CompletedAtUtc,
    DateTime? FailedAtUtc,
    string? FailureReason,
    DateTime CreatedAt);
