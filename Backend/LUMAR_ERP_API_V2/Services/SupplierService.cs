using LUMAR_ERP_API_V2.DTOs.Purchasing;
using LUMAR_ERP_API_V2.DTOs.Suppliers;
using LUMAR_ERP_API_V2.Repositories;

namespace LUMAR_ERP_API_V2.Services;

public sealed class SupplierService(ISupplierRepository supplierRepository, IPurchasingRepository purchasingRepository) : ISupplierService
{
    public Task<SupplierDetailsDto> CreateAsync(CreateSupplierRequestDto x, CancellationToken c) => supplierRepository.CreateAsync(x, c);

    public Task<IReadOnlyList<SupplierListDto>> GetAllAsync(CancellationToken c) => supplierRepository.GetAllAsync(c);

    public Task<SupplierDetailsDto?> GetByIdAsync(int i, CancellationToken c) => supplierRepository.GetByIdAsync(i, c);

    public Task<SupplierHomeSummaryDto?> GetHomeSummaryAsync(int supplierId, CancellationToken c) => supplierRepository.GetHomeSummaryAsync(supplierId, c);

    public Task<SupplierPaymentResponseStatusDto?> GetPaymentResponseStatusAsync(int supplierId, int paymentId, CancellationToken c)
        => supplierRepository.GetPaymentResponseStatusAsync(supplierId, paymentId, c);

    public Task<IReadOnlyList<SupplierTransactionDto>> GetTransactionsAsync(int i, CancellationToken c) => supplierRepository.GetTransactionsAsync(i, c);

    public Task<IReadOnlyList<SupplierLedgerEntryDto>> GetLedgerAsync(int i, CancellationToken c) => supplierRepository.GetLedgerAsync(i, c);

    public Task<IReadOnlyList<SupplierInvoiceDto>> GetInvoicesAsync(int i, CancellationToken c) => supplierRepository.GetInvoicesAsync(i, c);

    public async Task<SupplierInvoiceDto?> GetInvoiceAsync(int supplierId, int invoiceId, CancellationToken c)
    {
        var invoices = await supplierRepository.GetInvoicesAsync(supplierId, c);
        return invoices.FirstOrDefault(invoice => invoice.SupplierInvoiceId == invoiceId);
    }

    public Task<IReadOnlyList<SupplierPaymentDto>> GetPaymentsAsync(int i, CancellationToken c) => supplierRepository.GetPaymentsAsync(i, c);

    public async Task<SupplierPaymentDto?> GetPaymentAsync(int supplierId, int paymentId, CancellationToken c)
    {
        var payments = await supplierRepository.GetPaymentsAsync(supplierId, c);
        return payments.FirstOrDefault(payment => payment.SupplierPaymentId == paymentId);
    }

    public Task<IReadOnlyList<SupplierPaymentAllocationDto>> GetAllocationsAsync(int i, CancellationToken c) => supplierRepository.GetAllocationsAsync(i, c);

    public async Task<IReadOnlyList<GoodsReceiptDto>> GetGoodsReceiptsAsync(int supplierId, CancellationToken c)
    {
        var receipts = await purchasingRepository.GetReceiptsAsync(c);
        return receipts.Where(r => r.SupplierId == supplierId).ToList();
    }

    public async Task<GoodsReceiptDto?> GetGoodsReceiptAsync(int supplierId, int receiptId, CancellationToken c)
    {
        var receipt = await purchasingRepository.GetReceiptAsync(receiptId, c);
        return receipt is not null && receipt.SupplierId == supplierId ? receipt : null;
    }
}