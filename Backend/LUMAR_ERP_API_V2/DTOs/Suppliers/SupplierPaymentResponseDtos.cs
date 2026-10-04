namespace LUMAR_ERP_API_V2.DTOs.Suppliers;

public sealed record SupplierPaymentAcknowledgementDto(
    int SupplierPaymentAcknowledgementId,
    int SupplierPaymentId,
    int SupplierId,
    string Status,
    DateTime CreatedAt,
    DateTime? AcknowledgedAt,
    string? Notes,
    Guid? SourceOperationId);

public sealed record PaymentDisputeDto(
    int PaymentDisputeId,
    int SupplierPaymentId,
    int SupplierId,
    decimal? DisputedAmount,
    string Status,
    string? Reason,
    DateTime CreatedAt,
    DateTime? ResolvedAt,
    Guid? SourceOperationId);

public sealed record AcknowledgePaymentRequestDto(
    int SupplierPaymentId,
    int? RequestedSupplierId,
    string? Notes,
    Guid SourceOperationId);

public sealed record DisputePaymentRequestDto(
    int SupplierPaymentId,
    int? RequestedSupplierId,
    decimal? DisputedAmount,
    string Reason,
    Guid SourceOperationId);
