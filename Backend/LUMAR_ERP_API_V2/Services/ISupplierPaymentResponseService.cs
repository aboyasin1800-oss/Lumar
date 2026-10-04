using LUMAR_ERP_API_V2.DTOs.Suppliers;

namespace LUMAR_ERP_API_V2.Services;

public interface ISupplierPaymentResponseService
{
    Task<SupplierPaymentAcknowledgementDto> AcknowledgePaymentAsync(int supplierPaymentId, int? requestedSupplierId, string? notes, Guid sourceOperationId, CancellationToken cancellationToken = default);
    Task<PaymentDisputeDto> DisputePaymentAsync(int supplierPaymentId, int? requestedSupplierId, decimal? disputedAmount, string reason, Guid sourceOperationId, CancellationToken cancellationToken = default);
}
