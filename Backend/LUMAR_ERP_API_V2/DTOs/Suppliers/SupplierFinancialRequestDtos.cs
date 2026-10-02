using LUMAR_ERP_API_V2.FinancialFoundation;

namespace LUMAR_ERP_API_V2.DTOs.Suppliers;

public sealed record CreateSupplierInvoiceLineRequestDto(int? InventoryItemId, decimal Quantity, decimal UnitCost, int? RollCount = null, string? ItemDescription = null, string ItemType = "Legacy", string? SupplierItemCode = null, string? ProductType = null, string? UnitCode = null, decimal? ItemCount = null, decimal? ReceivedItemCount = null);

public sealed record CreateSupplierInvoiceRequestDto(int SupplierId, string InvoiceNumber, DateOnly InvoiceDate, DateOnly DueDate, decimal Amount, string? Notes, Guid SourceOperationId, string CurrencyCode = "YER", int? PurchaseOrderId = null, IReadOnlyList<CreateSupplierInvoiceLineRequestDto>? Lines = null);

public sealed record CreateSupplierPaymentRequestDto(int SupplierId, int? SupplierInvoiceId, decimal Amount, DateOnly PaymentDate, int CashAccountId, SupplierPaymentKind PaymentKind, string PaymentMethod, string ReferenceNumber, string? Notes, Guid SourceOperationId, string CurrencyCode = "YER");

public sealed record AllocateSupplierPaymentRequestDto(int SupplierPaymentId, int SupplierInvoiceId, decimal Amount, DateOnly AllocationDate, Guid SourceOperationId, string ReferenceNumber);

public sealed record ReverseSupplierFinancialRequestDto(string DocumentType, int DocumentId, Guid SourceOperationId, string Reason);

public sealed record SupplierFinancialReversalWorkflowResult(string DocumentType, int DocumentId, long AccountingEventId, Guid SourceOperationId);