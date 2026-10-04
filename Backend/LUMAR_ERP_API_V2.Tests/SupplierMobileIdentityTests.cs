using LUMAR_ERP_API_V2.Authorization;
using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.DTOs.Purchasing;
using LUMAR_ERP_API_V2.DTOs.Suppliers;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Services;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class SupplierMobileIdentityTests
{
    [Fact]
    public void Customer_account_requires_customer_owner_only()
    {
        Assert.True(MobileAccountIdentityValidator.IsValid("Customer", 11, null, null));
        Assert.False(MobileAccountIdentityValidator.IsValid("Customer", 11, 22, null));
        Assert.False(MobileAccountIdentityValidator.IsValid("Customer", null, null, 44));
    }

    [Fact]
    public void Employee_account_requires_employee_owner_only()
    {
        Assert.True(MobileAccountIdentityValidator.IsValid("Employee", null, 7, null));
        Assert.False(MobileAccountIdentityValidator.IsValid("Employee", 4, 7, null));
        Assert.False(MobileAccountIdentityValidator.IsValid("Employee", null, null, 44));
    }

    [Fact]
    public void Supplier_account_requires_supplier_owner_only()
    {
        Assert.True(MobileAccountIdentityValidator.IsValid("Supplier", null, null, 88));
        Assert.False(MobileAccountIdentityValidator.IsValid("Supplier", 4, null, 88));
        Assert.False(MobileAccountIdentityValidator.IsValid("Supplier", null, 9, 88));
    }

    [Fact]
    public void Missing_owner_is_rejected()
    {
        Assert.False(MobileAccountIdentityValidator.IsValid("Customer", null, null, null));
        Assert.False(MobileAccountIdentityValidator.IsValid("Employee", null, null, null));
        Assert.False(MobileAccountIdentityValidator.IsValid("Supplier", null, null, null));
    }

    [Fact]
    public void Multiple_owners_are_rejected()
    {
        Assert.False(MobileAccountIdentityValidator.IsValid("Customer", 1, 2, null));
        Assert.False(MobileAccountIdentityValidator.IsValid("Employee", 1, 2, null));
        Assert.False(MobileAccountIdentityValidator.IsValid("Supplier", 1, null, 2));
    }

    [Fact]
    public void Unsupported_account_types_are_rejected()
    {
        Assert.False(MobileAccountIdentityValidator.IsValid("Admin", 1, null, null));
        Assert.False(MobileAccountIdentityValidator.IsValid("", 1, null, null));
    }

    [Fact]
    public void Account_type_dto_supports_supplier_identity()
    {
        var dto = new AccountTypeDto("Supplier", null, null, 332);

        Assert.Equal("Supplier", dto.AccountType);
        Assert.Null(dto.CustomerId);
        Assert.Null(dto.EmployeeId);
        Assert.Equal(332, dto.SupplierId);
    }

    [Fact]
    public void Mobile_session_dto_supports_supplier_identity()
    {
        var dto = new MobileSessionDto("token", DateTime.UtcNow.AddHours(1), new CurrentUserDto(1, "supplier", "Supplier User", "Supplier", true, null, "Supplier", null, null, 12), "Supplier", null, null, 12);

        Assert.Equal("Supplier", dto.AccountType);
        Assert.Equal(12, dto.SupplierId);
    }

    [Fact]
    public void Supplier_owner_context_is_resolved_from_authenticated_account_only()
    {
        var user = new CurrentUserDto(10, "supplier_user", "Supplier User", "Supplier", true, null, "Supplier", null, null, 77);

        Assert.Equal("Supplier", user.AccountType);
        Assert.Equal(77, user.SupplierId);
        Assert.True(SupplierOwnershipGuard.IsAllowed(77, null));
        Assert.False(SupplierOwnershipGuard.IsAllowed(77, 99));
    }

    [Fact]
    public async Task Supplier_ownership_resolver_ignores_client_supplied_supplier_ids()
    {
        var resolver = new SupplierOwnershipResolver(new FixedAuthenticatedUserContext(new CurrentUserDto(10, "supplier_user", "Supplier User", "Supplier", true, null, "Supplier", null, null, 77)));

        var resolved = await resolver.ResolveCurrentSupplierAsync();

        Assert.Equal(77, resolved);
        Assert.True(resolver.CanAccessSupplier(77, 99) == false);
        Assert.True(resolver.CanAccessSupplier(77, 77));
    }

    [Fact]
    public async Task Supplier_ownership_resolver_rejects_customer_employee_and_anonymous_accounts()
    {
        var customerResolver = new SupplierOwnershipResolver(new FixedAuthenticatedUserContext(new CurrentUserDto(1, "cust", "Customer User", "Customer", true, null, "Customer", 55, null, null)));
        var employeeResolver = new SupplierOwnershipResolver(new FixedAuthenticatedUserContext(new CurrentUserDto(2, "emp", "Employee User", "Employee", true, null, "Employee", null, 33, null)));
        var anonymousResolver = new SupplierOwnershipResolver(new FixedAuthenticatedUserContext(null));

        Assert.Null(await customerResolver.ResolveCurrentSupplierAsync());
        Assert.Null(await employeeResolver.ResolveCurrentSupplierAsync());
        Assert.Null(await anonymousResolver.ResolveCurrentSupplierAsync());
    }

    [Fact]
    public async Task Supplier_read_service_filters_invoice_payment_and_goods_receipt_by_authenticated_supplier_only()
    {
        var service = new SupplierService(new StubSupplierRepository(), new StubPurchasingRepository());

        var invoice = await service.GetInvoiceAsync(77, 100, default);
        var payment = await service.GetPaymentAsync(77, 200, default);
        var receipt = await service.GetGoodsReceiptAsync(77, 300, default);

        Assert.NotNull(invoice);
        Assert.Equal(77, invoice!.SupplierId);
        Assert.NotNull(payment);
        Assert.Equal(77, payment!.SupplierId);
        Assert.NotNull(receipt);
        Assert.Equal(77, receipt!.SupplierId);

        Assert.Null(await service.GetInvoiceAsync(77, 999, default));
        Assert.Null(await service.GetPaymentAsync(77, 999, default));
        Assert.Null(await service.GetGoodsReceiptAsync(77, 999, default));
    }

    private sealed class FixedAuthenticatedUserContext(CurrentUserDto? user) : IAuthenticatedUserContext
    {
        public Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default) => Task.FromResult(user);
    }

    private sealed class StubSupplierRepository : ISupplierRepository
    {
        public Task<SupplierDetailsDto> CreateAsync(CreateSupplierRequestDto supplier, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<SupplierListDto>> GetAllAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<SupplierListDto>>(Array.Empty<SupplierListDto>());
        public Task<SupplierDetailsDto?> GetByIdAsync(int id, CancellationToken ct) => Task.FromResult<SupplierDetailsDto?>(null);
        public Task<SupplierHomeSummaryDto?> GetHomeSummaryAsync(int supplierId, CancellationToken ct) => Task.FromResult<SupplierHomeSummaryDto?>(null);
        public Task<SupplierPaymentResponseStatusDto?> GetPaymentResponseStatusAsync(int supplierId, int paymentId, CancellationToken ct) => Task.FromResult<SupplierPaymentResponseStatusDto?>(null);
        public Task<IReadOnlyList<SupplierTransactionDto>> GetTransactionsAsync(int id, CancellationToken ct) => Task.FromResult<IReadOnlyList<SupplierTransactionDto>>(Array.Empty<SupplierTransactionDto>());
        public Task<IReadOnlyList<SupplierLedgerEntryDto>> GetLedgerAsync(int id, CancellationToken ct) => Task.FromResult<IReadOnlyList<SupplierLedgerEntryDto>>(Array.Empty<SupplierLedgerEntryDto>());
        public Task<IReadOnlyList<SupplierInvoiceDto>> GetInvoicesAsync(int id, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<SupplierInvoiceDto>>(
                id == 77
                    ? new[] { new SupplierInvoiceDto(100, 77, 10, "INV-100", DateTime.UtcNow, DateTime.UtcNow.AddDays(7), 100m, 0m, "Open", "ok", DateTime.UtcNow) }
                    : Array.Empty<SupplierInvoiceDto>());
        public Task<IReadOnlyList<SupplierPaymentDto>> GetPaymentsAsync(int id, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<SupplierPaymentDto>>(
                id == 77
                    ? new[] { new SupplierPaymentDto(200, 77, "PAY-200", DateTime.UtcNow, 150m, "Bank", "REF-200", "notes", DateTime.UtcNow, null) }
                    : Array.Empty<SupplierPaymentDto>());
        public Task<IReadOnlyList<SupplierPaymentAllocationDto>> GetAllocationsAsync(int id, CancellationToken ct) => Task.FromResult<IReadOnlyList<SupplierPaymentAllocationDto>>(Array.Empty<SupplierPaymentAllocationDto>());
    }

    private sealed class StubPurchasingRepository : IPurchasingRepository
    {
        public Task<IReadOnlyList<PurchaseOrderListDto>> GetOrdersAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<PurchaseOrderListDto>>(Array.Empty<PurchaseOrderListDto>());
        public Task<PurchaseOrderDetailsDto?> GetOrderAsync(int id, CancellationToken ct) => Task.FromResult<PurchaseOrderDetailsDto?>(null);
        public Task<IReadOnlyList<PurchaseOrderItemDto>> GetOrderItemsAsync(int id, CancellationToken ct) => Task.FromResult<IReadOnlyList<PurchaseOrderItemDto>>(Array.Empty<PurchaseOrderItemDto>());
        public Task<IReadOnlyList<GoodsReceiptDto>> GetOrderReceiptsAsync(int id, CancellationToken ct) => Task.FromResult<IReadOnlyList<GoodsReceiptDto>>(Array.Empty<GoodsReceiptDto>());
        public Task<IReadOnlyList<GoodsReceiptDto>> GetReceiptsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<GoodsReceiptDto>>(Array.Empty<GoodsReceiptDto>());
        public Task<GoodsReceiptDto?> GetReceiptAsync(int id, CancellationToken ct)
            => Task.FromResult<GoodsReceiptDto?>(id == 300 ? new GoodsReceiptDto(300, 77, 10, "GR-300", DateTime.UtcNow, "ok", DateTime.UtcNow) : null);
        public Task<IReadOnlyList<GoodsReceiptItemDto>> GetReceiptItemsAsync(int id, CancellationToken ct) => Task.FromResult<IReadOnlyList<GoodsReceiptItemDto>>(Array.Empty<GoodsReceiptItemDto>());
        public Task<IReadOnlyList<PurchasingInvoiceDto>> GetInvoicesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<PurchasingInvoiceDto>>(Array.Empty<PurchasingInvoiceDto>());
        public Task<PurchasingInvoiceDto?> GetInvoiceAsync(int id, CancellationToken ct) => Task.FromResult<PurchasingInvoiceDto?>(null);
        public Task<IReadOnlyList<PurchasingPaymentDto>> GetPaymentsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<PurchasingPaymentDto>>(Array.Empty<PurchasingPaymentDto>());
        public Task<PurchasingPaymentDto?> GetPaymentAsync(int id, CancellationToken ct) => Task.FromResult<PurchasingPaymentDto?>(null);
        public Task<IReadOnlyList<PurchasingPaymentAllocationDto>> GetAllocationsAsync(int id, CancellationToken ct) => Task.FromResult<IReadOnlyList<PurchasingPaymentAllocationDto>>(Array.Empty<PurchasingPaymentAllocationDto>());
    }
}
