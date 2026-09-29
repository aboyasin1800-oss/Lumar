using LUMAR_ERP_API_V2.Authorization;
using LUMAR_ERP_API_V2.Controllers;
using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.DTOs.Suppliers;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
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

    private static SuppliersController Controller(ISupplierService service, CurrentUserDto? user) => new(service, new FakeUserContext(user), new Es7OperationalAudit(NullLogger<Es7OperationalAudit>.Instance));
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
        public Task<IReadOnlyList<SupplierTransactionDto>> GetTransactionsAsync(int id, CancellationToken ct) => Task.FromResult<IReadOnlyList<SupplierTransactionDto>>([]);
        public Task<IReadOnlyList<SupplierLedgerEntryDto>> GetLedgerAsync(int id, CancellationToken ct) => Task.FromResult<IReadOnlyList<SupplierLedgerEntryDto>>([]);
        public Task<IReadOnlyList<SupplierInvoiceDto>> GetInvoicesAsync(int id, CancellationToken ct) => Task.FromResult<IReadOnlyList<SupplierInvoiceDto>>([]);
        public Task<IReadOnlyList<SupplierPaymentDto>> GetPaymentsAsync(int id, CancellationToken ct) => Task.FromResult<IReadOnlyList<SupplierPaymentDto>>([]);
        public Task<IReadOnlyList<SupplierPaymentAllocationDto>> GetAllocationsAsync(int id, CancellationToken ct) => Task.FromResult<IReadOnlyList<SupplierPaymentAllocationDto>>([]);
    }
}