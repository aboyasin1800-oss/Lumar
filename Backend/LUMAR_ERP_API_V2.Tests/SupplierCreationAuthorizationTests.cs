using LUMAR_ERP_API_V2.Authorization;
using LUMAR_ERP_API_V2.Controllers;
using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.DTOs.Purchasing;
using LUMAR_ERP_API_V2.DTOs.Suppliers;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class SupplierCreationAuthorizationTests
{
    [Fact]
    public async Task Anonymous_request_is_rejected_before_service()
    {
        var service = new FakeSupplierService();
        var controller = Controller(service, null);

        var result = await controller.Create(Request(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result.Result);
        Assert.False(service.CreateCalled);
    }

    [Fact]
    public async Task Financial_manager_cannot_create_supplier()
    {
        var service = new FakeSupplierService();
        var controller = Controller(service, User("Authorized Financial Manager"));

        var result = await controller.Create(Request(), CancellationToken.None);

        Assert.IsType<ForbidResult>(result.Result);
        Assert.False(service.CreateCalled);
    }

    [Fact]
    public async Task System_administrator_can_create_supplier()
    {
        var service = new FakeSupplierService();
        var controller = Controller(service, User("System Administrator"));

        var result = await controller.Create(Request(), CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(41, ((SupplierDetailsDto)created.Value!).SupplierId);
        Assert.True(service.CreateCalled);
    }

    [Fact]
    public async Task Ordinary_role_can_create_supplier_with_test_grant()
    {
        var service = new FakeSupplierService();
        var user = User("Admin");
        var testMode = new Es7OperationalTestMode(Options.Create(new Es7OperationalTestModeOptions { Enabled = true, GrantMinutes = 30 }));
        var grant = testMode.Activate(user, "session-a");
        var controller = new SuppliersController(service, new FakeUserContext(user), new FakeSupplierOwnershipResolver(), new Es7OperationalAudit(NullLogger<Es7OperationalAudit>.Instance), testMode)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.Request.Headers.Authorization = "Bearer session-a";
        controller.Request.Headers[Es7OperationalTestMode.GrantHeaderName] = grant.GrantToken;

        var result = await controller.Create(Request(), CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.True(service.CreateCalled);
    }

    private static SuppliersController Controller(ISupplierService service, CurrentUserDto? user) => new(service, new FakeUserContext(user), new FakeSupplierOwnershipResolver(), new Es7OperationalAudit(NullLogger<Es7OperationalAudit>.Instance));
    private static CreateSupplierRequestDto Request() => new() { SupplierCode = "SUP-41", SupplierName = "مورد اختبار", SourceOperationId = Guid.NewGuid() };
    private static CurrentUserDto User(string role) => new(7, "es7-user", "ES7 User", role, true, null);

    private sealed class FakeUserContext(CurrentUserDto? user) : IAuthenticatedUserContext
    {
        public Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default) => Task.FromResult(user);
    }

    private sealed class FakeSupplierService : ISupplierService
    {
        public bool CreateCalled { get; private set; }
        public Task<SupplierDetailsDto> CreateAsync(CreateSupplierRequestDto supplier, CancellationToken ct) { CreateCalled = true; return Task.FromResult(new SupplierDetailsDto(41,supplier.SupplierCode,supplier.SupplierName,supplier.Phone,supplier.Email,supplier.Address,true,DateTime.UtcNow,null)); }
        public Task<IReadOnlyList<SupplierListDto>> GetAllAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<SupplierListDto>>([]);
        public Task<SupplierDetailsDto?> GetByIdAsync(int id, CancellationToken ct) => Task.FromResult<SupplierDetailsDto?>(null);
        public Task<SupplierHomeSummaryDto?> GetHomeSummaryAsync(int supplierId, CancellationToken ct) => Task.FromResult<SupplierHomeSummaryDto?>(null);
        public Task<SupplierPaymentResponseStatusDto?> GetPaymentResponseStatusAsync(int supplierId, int paymentId, CancellationToken ct) => Task.FromResult<SupplierPaymentResponseStatusDto?>(null);
        public Task<IReadOnlyList<SupplierTransactionDto>> GetTransactionsAsync(int id, CancellationToken ct) => Task.FromResult<IReadOnlyList<SupplierTransactionDto>>([]);
        public Task<IReadOnlyList<SupplierLedgerEntryDto>> GetLedgerAsync(int id, CancellationToken ct) => Task.FromResult<IReadOnlyList<SupplierLedgerEntryDto>>([]);
        public Task<IReadOnlyList<SupplierInvoiceDto>> GetInvoicesAsync(int id, CancellationToken ct) => Task.FromResult<IReadOnlyList<SupplierInvoiceDto>>([]);
        public Task<SupplierInvoiceDto?> GetInvoiceAsync(int supplierId, int invoiceId, CancellationToken ct) => Task.FromResult<SupplierInvoiceDto?>(null);
        public Task<IReadOnlyList<SupplierPaymentDto>> GetPaymentsAsync(int id, CancellationToken ct) => Task.FromResult<IReadOnlyList<SupplierPaymentDto>>([]);
        public Task<SupplierPaymentDto?> GetPaymentAsync(int supplierId, int paymentId, CancellationToken ct) => Task.FromResult<SupplierPaymentDto?>(null);
        public Task<IReadOnlyList<SupplierPaymentAllocationDto>> GetAllocationsAsync(int id, CancellationToken ct) => Task.FromResult<IReadOnlyList<SupplierPaymentAllocationDto>>([]);
        public Task<IReadOnlyList<GoodsReceiptDto>> GetGoodsReceiptsAsync(int supplierId, CancellationToken ct) => Task.FromResult<IReadOnlyList<GoodsReceiptDto>>([]);
        public Task<GoodsReceiptDto?> GetGoodsReceiptAsync(int supplierId, int receiptId, CancellationToken ct) => Task.FromResult<GoodsReceiptDto?>(null);
    }

    private sealed class FakeSupplierOwnershipResolver : ISupplierOwnershipResolver
    {
        public Task<int?> ResolveCurrentSupplierAsync(CancellationToken cancellationToken = default) => Task.FromResult<int?>(null);
        public Task<int?> ResolveCurrentSupplier(CancellationToken cancellationToken = default) => Task.FromResult<int?>(null);
        public Task<int> RequireCurrentSupplierAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public bool CanAccessSupplier(int? currentSupplierId, int? requestedSupplierId) => true;
    }
}