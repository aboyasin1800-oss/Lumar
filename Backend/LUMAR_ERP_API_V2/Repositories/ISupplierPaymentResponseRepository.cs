using LUMAR_ERP_API_V2.DTOs.Suppliers;

namespace LUMAR_ERP_API_V2.Repositories;

public interface ISupplierPaymentResponseRepository
{
    Task<SupplierPaymentDto?> GetPaymentAsync(int supplierPaymentId, CancellationToken cancellationToken);
    Task<SupplierPaymentAcknowledgementDto?> GetAcknowledgementAsync(int supplierPaymentId, CancellationToken cancellationToken);
    Task<PaymentDisputeDto?> GetDisputeAsync(int supplierPaymentId, CancellationToken cancellationToken);
    Task<SupplierPaymentAcknowledgementDto> CreateAcknowledgementAsync(int supplierPaymentId, int supplierId, string? notes, Guid sourceOperationId, CancellationToken cancellationToken);
    Task<PaymentDisputeDto> CreateDisputeAsync(int supplierPaymentId, int supplierId, decimal? disputedAmount, string reason, Guid sourceOperationId, CancellationToken cancellationToken);
}
