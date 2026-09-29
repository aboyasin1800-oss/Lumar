using LUMAR_ERP_API_V2.DTOs.Purchasing;
using LUMAR_ERP_API_V2.Repositories;

namespace LUMAR_ERP_API_V2.Services;

public interface ISupplierInvoiceLineService
{
    Task<IReadOnlyList<PurchasingInvoiceLineDto>?> GetForInvoiceAsync(int invoiceId, CancellationToken cancellationToken);
}

public sealed class SupplierInvoiceLineService(ISupplierInvoiceLineRepository repository) : ISupplierInvoiceLineService
{
    public Task<IReadOnlyList<PurchasingInvoiceLineDto>?> GetForInvoiceAsync(int invoiceId, CancellationToken cancellationToken) =>
        repository.GetForInvoiceAsync(invoiceId, cancellationToken);
}