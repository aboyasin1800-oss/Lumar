using LUMAR_ERP_API_V2.Authorization;
using LUMAR_ERP_API_V2.Controllers;
using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.DTOs.Suppliers;
using LUMAR_ERP_API_V2.FinancialFoundation;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class Es7OperationalApiAuthorizationTests
{
    [Fact]
    public async Task Anonymous_supplier_invoice_request_is_rejected_before_coordinator()
    {
        var coordinator = new FakeSupplierFinancialCoordinator();
        var controller = new SupplierFinancialOperationsController(coordinator, new FakeUserContext(null));

        var result = await controller.CreateInvoice(InvoiceRequest(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result.Result);
        Assert.False(coordinator.InvoiceCalled);
    }

    [Fact]
    public async Task Authorized_financial_manager_can_create_supplier_invoice()
    {
        var coordinator = new FakeSupplierFinancialCoordinator();
        var controller = new SupplierFinancialOperationsController(coordinator, new FakeUserContext(User("Authorized Financial Manager")));

        var result = await controller.CreateInvoice(InvoiceRequest(), CancellationToken.None);

        Assert.Equal(StatusCodes.Status201Created, ((ObjectResult)result.Result!).StatusCode);
        Assert.True(coordinator.InvoiceCalled);
    }

    [Fact]
    public async Task Ordinary_role_cannot_reverse_supplier_document()
    {
        var coordinator = new FakeSupplierFinancialCoordinator();
        var controller = new SupplierFinancialOperationsController(coordinator, new FakeUserContext(User("Purchasing Manager")));

        var result = await controller.Reverse(new ReverseSupplierFinancialRequestDto("Invoice", 10, Guid.NewGuid(), "Correction"), CancellationToken.None);

        Assert.IsType<ForbidResult>(result.Result);
        Assert.False(coordinator.ReversalCalled);
    }

    [Fact]
    public async Task Ordinary_role_can_execute_with_test_grant_bound_to_current_session()
    {
        var user = User("Purchasing Manager");
        var testMode = TestMode();
        var grant = testMode.Activate(user, "session-a");
        var coordinator = new FakeSupplierFinancialCoordinator();
        var controller = new SupplierFinancialOperationsController(coordinator, new FakeUserContext(user), testMode)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.Request.Headers.Authorization = "Bearer session-a";
        controller.Request.Headers[Es7OperationalTestMode.GrantHeaderName] = grant.GrantToken;

        var result = await controller.CreateInvoice(InvoiceRequest(), CancellationToken.None);

        Assert.Equal(StatusCodes.Status201Created, ((ObjectResult)result.Result!).StatusCode);
        Assert.True(coordinator.InvoiceCalled);
    }

    [Fact]
    public void Test_grant_cannot_be_reused_with_another_session()
    {
        var user = User("Purchasing Manager");
        var testMode = TestMode();
        var grant = testMode.Activate(user, "session-a");

        Assert.False(testMode.HasActiveGrant(user, "session-b", grant.GrantToken));
    }

    private static CreateSupplierInvoiceRequestDto InvoiceRequest() => new(7, "ES7-API-INV", new DateOnly(2026, 9, 29), new DateOnly(2026, 10, 1), 100m, null, Guid.NewGuid(), Lines: [new(11, 2m, 50m)]);
    private static CurrentUserDto User(string role) => new(7, "es7-user", "ES7 User", role, true, null);
    private static Es7OperationalTestMode TestMode() => new(Options.Create(new Es7OperationalTestModeOptions { Enabled = true, GrantMinutes = 30 }));

    private sealed class FakeUserContext(CurrentUserDto? user) : IAuthenticatedUserContext
    {
        public Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default) => Task.FromResult(user);
    }

    private sealed class FakeSupplierFinancialCoordinator : ISupplierFinancialWorkflowCoordinator
    {
        public bool InvoiceCalled { get; private set; }
        public bool ReversalCalled { get; private set; }

        public Task<SupplierFinancialInvoiceResult> CreateInvoiceAsync(CreateSupplierInvoiceRequestDto request, CurrentUserDto user, string correlationId, CancellationToken cancellationToken)
        {
            InvoiceCalled = true;
            return Task.FromResult(new SupplierFinancialInvoiceResult(10, 20, 0m, request.Amount, false));
        }

        public Task<SupplierFinancialPaymentResult> CreatePaymentAsync(CreateSupplierPaymentRequestDto request, CurrentUserDto user, string correlationId, CancellationToken cancellationToken) => Task.FromResult(new SupplierFinancialPaymentResult(11, 21, 0m, 0m, false));
        public Task<SupplierFinancialAllocationResult> AllocateAsync(AllocateSupplierPaymentRequestDto request, CurrentUserDto user, string correlationId, CancellationToken cancellationToken) => Task.FromResult(new SupplierFinancialAllocationResult(12, 22, request.Amount, false));

        public Task<SupplierFinancialReversalWorkflowResult> ReverseAsync(ReverseSupplierFinancialRequestDto request, CurrentUserDto user, string correlationId, CancellationToken cancellationToken)
        {
            ReversalCalled = true;
            return Task.FromResult(new SupplierFinancialReversalWorkflowResult(request.DocumentType, request.DocumentId, 23, request.SourceOperationId));
        }
    }
}