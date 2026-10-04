using LUMAR_ERP_API_V2.Authorization;
using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.DTOs.Suppliers;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class SupplierPaymentResponseTests
{
    [Fact]
    public async Task Supplier_payment_acknowledgement_uses_authenticated_supplier_only()
    {
        var service = new SupplierPaymentResponseService(
            new FakeSupplierPaymentResponseRepository(new SupplierPaymentDto(42, 77, "PAY-42", DateTime.UtcNow, 150m, "Bank", "REF-42", "Settlement", DateTime.UtcNow, null)),
            new FakeSupplierOwnershipResolver(77),
            new FixedAuthenticatedUserContext(new CurrentUserDto(9, "supplier_user", "Supplier User", "Supplier", true, null, "Supplier", null, null, 77)),
            new Es7OperationalAudit(NullLogger<Es7OperationalAudit>.Instance));

        var acknowledgement = await service.AcknowledgePaymentAsync(42, 99, "Received", Guid.NewGuid());

        Assert.Equal(77, acknowledgement.SupplierId);
        Assert.Equal("Acknowledged", acknowledgement.Status);
    }

    [Fact]
    public async Task Supplier_payment_dispute_ignores_client_supplier_id_and_uses_authenticated_owner()
    {
        var service = new SupplierPaymentResponseService(
            new FakeSupplierPaymentResponseRepository(new SupplierPaymentDto(43, 77, "PAY-43", DateTime.UtcNow, 210m, "Bank", "REF-43", "Late settlement", DateTime.UtcNow, null)),
            new FakeSupplierOwnershipResolver(77),
            new FixedAuthenticatedUserContext(new CurrentUserDto(9, "supplier_user", "Supplier User", "Supplier", true, null, "Supplier", null, null, 77)),
            new Es7OperationalAudit(NullLogger<Es7OperationalAudit>.Instance));

        var dispute = await service.DisputePaymentAsync(43, 88, 110m, "Payment amount was incorrect", Guid.NewGuid());

        Assert.Equal(77, dispute.SupplierId);
        Assert.Equal(110m, dispute.DisputedAmount);
        Assert.Equal("Open", dispute.Status);
    }

    private sealed class FixedAuthenticatedUserContext(CurrentUserDto user) : IAuthenticatedUserContext
    {
        public Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default) => Task.FromResult<CurrentUserDto?>(user);
    }

    private sealed class FakeSupplierOwnershipResolver(int supplierId) : ISupplierOwnershipResolver
    {
        public Task<int?> ResolveCurrentSupplierAsync(CancellationToken cancellationToken = default) => Task.FromResult<int?>(supplierId);

        public Task<int?> ResolveCurrentSupplier(CancellationToken cancellationToken = default) => Task.FromResult<int?>(supplierId);

        public Task<int> RequireCurrentSupplierAsync(CancellationToken cancellationToken = default) => Task.FromResult(supplierId);

        public bool CanAccessSupplier(int? currentSupplierId, int? requestedSupplierId)
            => currentSupplierId == supplierId && requestedSupplierId == supplierId;
    }

    private sealed class FakeSupplierPaymentResponseRepository(SupplierPaymentDto payment) : ISupplierPaymentResponseRepository
    {
        public Task<SupplierPaymentDto?> GetPaymentAsync(int supplierPaymentId, CancellationToken cancellationToken)
            => Task.FromResult<SupplierPaymentDto?>(supplierPaymentId == payment.SupplierPaymentId ? payment : null);

        public Task<SupplierPaymentAcknowledgementDto?> GetAcknowledgementAsync(int supplierPaymentId, CancellationToken cancellationToken)
            => Task.FromResult<SupplierPaymentAcknowledgementDto?>(null);

        public Task<PaymentDisputeDto?> GetDisputeAsync(int supplierPaymentId, CancellationToken cancellationToken)
            => Task.FromResult<PaymentDisputeDto?>(null);

        public Task<SupplierPaymentAcknowledgementDto> CreateAcknowledgementAsync(int supplierPaymentId, int supplierId, string? notes, Guid sourceOperationId, CancellationToken cancellationToken)
            => Task.FromResult(new SupplierPaymentAcknowledgementDto(1, supplierPaymentId, supplierId, "Acknowledged", DateTime.UtcNow, DateTime.UtcNow, notes, sourceOperationId));

        public Task<PaymentDisputeDto> CreateDisputeAsync(int supplierPaymentId, int supplierId, decimal? disputedAmount, string reason, Guid sourceOperationId, CancellationToken cancellationToken)
            => Task.FromResult(new PaymentDisputeDto(1, supplierPaymentId, supplierId, disputedAmount, "Open", reason, DateTime.UtcNow, null, sourceOperationId));
    }
}
