using System.ComponentModel.DataAnnotations;

namespace LUMAR_ERP_API_V2.DTOs.Suppliers;

public sealed record CreateSupplierRequestDto
{
	[Required, MaxLength(50)]
	public required string SupplierCode { get; init; }

	[Required, MaxLength(200)]
	public required string SupplierName { get; init; }

	[MaxLength(50)]
	public string? Phone { get; init; }

	[EmailAddress, MaxLength(200)]
	public string? Email { get; init; }

	[MaxLength(500)]
	public string? Address { get; init; }

	public required Guid SourceOperationId { get; init; }
}

public sealed record SupplierListDto(int SupplierId, string SupplierCode, string SupplierName, string? Phone, string? Email, bool IsActive);
public sealed record SupplierDetailsDto(int SupplierId, string SupplierCode, string SupplierName, string? Phone, string? Email, string? Address, bool IsActive, DateTime CreatedAt, DateTime? UpdatedAt);
public sealed record SupplierHomeSummaryDto(decimal CurrentBalance, int InvoiceCount, int PaymentCount, int GoodsReceiptCount, int PendingAcknowledgements, int OpenDisputes);
public sealed record SupplierTransactionDto(int SupplierTransactionId, int SupplierId, string ReferenceNumber, string TransactionType, decimal Amount, string? Description, DateTime CreatedAt);
public sealed record SupplierLedgerEntryDto(int SupplierLedgerEntryId, int SupplierId, string ReferenceNumber, decimal DebitAmount, decimal CreditAmount, decimal BalanceAfterTransaction, DateTime CreatedAt);
public sealed record SupplierInvoiceDto(int SupplierInvoiceId, int SupplierId, int? PurchaseOrderId, string InvoiceNumber, DateTime InvoiceDate, DateTime DueDate, decimal TotalAmount, decimal AmountPaid, string Status, string? Notes, DateTime CreatedAt);
public sealed record SupplierPaymentDto(int SupplierPaymentId, int SupplierId, string PaymentNumber, DateTime PaymentDate, decimal Amount, string? PaymentMethod, string? ReferenceNumber, string? Notes, DateTime CreatedAt, int? JournalEntryId);
public sealed record SupplierPaymentAllocationDto(int SupplierPaymentAllocationId, int SupplierPaymentId, int SupplierInvoiceId, decimal AllocatedAmount, DateTime AllocationDate, string? Notes, DateTime CreatedAt);
public sealed record SupplierPaymentResponseStatusDto(int SupplierPaymentId, int SupplierId, string PaymentNumber, string AcknowledgementStatus, string DisputeStatus, decimal? DisputedAmount, DateTime? AcknowledgedAt, DateTime? ResolvedAt, DateTime CreatedAt);