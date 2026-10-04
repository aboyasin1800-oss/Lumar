using LUMAR_ERP_API_V2.DTOs.Purchasing;
using LUMAR_ERP_API_V2.DTOs.Suppliers;

namespace LUMAR_ERP_API_V2.Services;

public interface ISupplierService
{
    Task<SupplierDetailsDto> CreateAsync(CreateSupplierRequestDto supplier, CancellationToken ct);
    Task<IReadOnlyList<SupplierListDto>> GetAllAsync(CancellationToken ct);
    Task<SupplierDetailsDto?> GetByIdAsync(int id, CancellationToken ct);
    Task<SupplierHomeSummaryDto?> GetHomeSummaryAsync(int supplierId, CancellationToken ct);
    Task<SupplierPaymentResponseStatusDto?> GetPaymentResponseStatusAsync(int supplierId, int paymentId, CancellationToken ct);
    Task<IReadOnlyList<SupplierTransactionDto>> GetTransactionsAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<SupplierLedgerEntryDto>> GetLedgerAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<SupplierInvoiceDto>> GetInvoicesAsync(int id, CancellationToken ct);
    Task<SupplierInvoiceDto?> GetInvoiceAsync(int supplierId, int invoiceId, CancellationToken ct);
    Task<IReadOnlyList<SupplierPaymentDto>> GetPaymentsAsync(int id, CancellationToken ct);
    Task<SupplierPaymentDto?> GetPaymentAsync(int supplierId, int paymentId, CancellationToken ct);
    Task<IReadOnlyList<SupplierPaymentAllocationDto>> GetAllocationsAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<GoodsReceiptDto>> GetGoodsReceiptsAsync(int supplierId, CancellationToken ct);
    Task<GoodsReceiptDto?> GetGoodsReceiptAsync(int supplierId, int receiptId, CancellationToken ct);
}