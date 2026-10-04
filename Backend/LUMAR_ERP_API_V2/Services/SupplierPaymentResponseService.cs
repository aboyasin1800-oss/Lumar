using LUMAR_ERP_API_V2.Authorization;
using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.DTOs.Suppliers;
using LUMAR_ERP_API_V2.Repositories;

namespace LUMAR_ERP_API_V2.Services;

public sealed class SupplierPaymentResponseService(
    ISupplierPaymentResponseRepository repository,
    ISupplierOwnershipResolver supplierOwnershipResolver,
    IAuthenticatedUserContext userContext,
    Es7OperationalAudit audit) : ISupplierPaymentResponseService
{
    public async Task<SupplierPaymentAcknowledgementDto> AcknowledgePaymentAsync(int supplierPaymentId, int? requestedSupplierId, string? notes, Guid sourceOperationId, CancellationToken cancellationToken = default)
    {
        if (supplierPaymentId <= 0)
            throw new ArgumentOutOfRangeException(nameof(supplierPaymentId));

        if (sourceOperationId == Guid.Empty)
            throw new ArgumentException("Source operation id is required.", nameof(sourceOperationId));

        var user = await userContext.GetCurrentUserAsync(cancellationToken)
            ?? throw new UnauthorizedAccessException("No authenticated user is available for supplier payment acknowledgement.");

        var currentSupplierId = await supplierOwnershipResolver.ResolveCurrentSupplierAsync(cancellationToken);
        if (!currentSupplierId.HasValue)
            throw new InvalidOperationException("Only a supplier account may acknowledge its payment.");

        _ = requestedSupplierId;

        var payment = await repository.GetPaymentAsync(supplierPaymentId, cancellationToken)
            ?? throw new InvalidOperationException("Supplier payment was not found.");

        if (payment.SupplierId != currentSupplierId.Value)
            throw new InvalidOperationException("Supplier ownership mismatch detected for the payment acknowledgement.");

        var acknowledgement = await repository.CreateAcknowledgementAsync(supplierPaymentId, currentSupplierId.Value, notes, sourceOperationId, cancellationToken);
        audit.Record(user, "SupplierPaymentAcknowledged", sourceOperationId, acknowledgement.SupplierPaymentAcknowledgementId, sourceOperationId.ToString("N"));
        return acknowledgement;
    }

    public async Task<PaymentDisputeDto> DisputePaymentAsync(int supplierPaymentId, int? requestedSupplierId, decimal? disputedAmount, string reason, Guid sourceOperationId, CancellationToken cancellationToken = default)
    {
        if (supplierPaymentId <= 0)
            throw new ArgumentOutOfRangeException(nameof(supplierPaymentId));

        if (sourceOperationId == Guid.Empty)
            throw new ArgumentException("Source operation id is required.", nameof(sourceOperationId));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Payment dispute reason is required.", nameof(reason));

        var user = await userContext.GetCurrentUserAsync(cancellationToken)
            ?? throw new UnauthorizedAccessException("No authenticated user is available for supplier payment dispute.");

        var currentSupplierId = await supplierOwnershipResolver.ResolveCurrentSupplierAsync(cancellationToken);
        if (!currentSupplierId.HasValue)
            throw new InvalidOperationException("Only a supplier account may dispute its payment.");

        _ = requestedSupplierId;

        var payment = await repository.GetPaymentAsync(supplierPaymentId, cancellationToken)
            ?? throw new InvalidOperationException("Supplier payment was not found.");

        if (payment.SupplierId != currentSupplierId.Value)
            throw new InvalidOperationException("Supplier ownership mismatch detected for the payment dispute.");

        var dispute = await repository.CreateDisputeAsync(supplierPaymentId, currentSupplierId.Value, disputedAmount ?? payment.Amount, reason, sourceOperationId, cancellationToken);
        audit.Record(user, "SupplierPaymentDisputed", sourceOperationId, dispute.PaymentDisputeId, sourceOperationId.ToString("N"));
        return dispute;
    }
}
