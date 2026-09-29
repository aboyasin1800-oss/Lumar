using LUMAR_ERP_API_V2.Authorization;
using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.DTOs.Suppliers;
using LUMAR_ERP_API_V2.FinancialFoundation;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("purchasing/operations")]
public sealed class SupplierFinancialOperationsController(
    ISupplierFinancialWorkflowCoordinator coordinator,
    IAuthenticatedUserContext userContext) : ControllerBase
{
    [HttpPost("invoices")]
    public async Task<ActionResult<SupplierFinancialInvoiceResult>> CreateInvoice(CreateSupplierInvoiceRequestDto request, CancellationToken cancellationToken)
    {
        if (request.SupplierId <= 0 || string.IsNullOrWhiteSpace(request.InvoiceNumber) || request.Amount <= 0 || request.SourceOperationId == Guid.Empty || request.InvoiceDate > request.DueDate)
            return BadRequest("Supplier invoice data is incomplete.");
        var auth = await AuthorizeAsync(Es7Permission.PurchasingInvoice, cancellationToken);
        if (auth.Error is not null) return auth.Error;
        return StatusCode(StatusCodes.Status201Created, await coordinator.CreateInvoiceAsync(request, auth.User!, CorrelationId, cancellationToken));
    }

    [HttpPost("payments")]
    public async Task<ActionResult<SupplierFinancialPaymentResult>> CreatePayment(CreateSupplierPaymentRequestDto request, CancellationToken cancellationToken)
    {
        if (request.SupplierId <= 0 || request.Amount <= 0 || request.CashAccountId <= 0 || request.SourceOperationId == Guid.Empty || string.IsNullOrWhiteSpace(request.PaymentMethod) || string.IsNullOrWhiteSpace(request.ReferenceNumber))
            return BadRequest("Supplier payment data is incomplete.");
        var auth = await AuthorizeAsync(Es7Permission.FinanceSupplierPay, cancellationToken);
        if (auth.Error is not null) return auth.Error;
        return StatusCode(StatusCodes.Status201Created, await coordinator.CreatePaymentAsync(request, auth.User!, CorrelationId, cancellationToken));
    }

    [HttpPost("allocations")]
    public async Task<ActionResult<SupplierFinancialAllocationResult>> CreateAllocation(AllocateSupplierPaymentRequestDto request, CancellationToken cancellationToken)
    {
        if (request.SupplierPaymentId <= 0 || request.SupplierInvoiceId <= 0 || request.Amount <= 0 || request.SourceOperationId == Guid.Empty || string.IsNullOrWhiteSpace(request.ReferenceNumber))
            return BadRequest("Supplier payment allocation data is incomplete.");
        var auth = await AuthorizeAsync(Es7Permission.PurchasingAllocate, cancellationToken);
        if (auth.Error is not null) return auth.Error;
        return StatusCode(StatusCodes.Status201Created, await coordinator.AllocateAsync(request, auth.User!, CorrelationId, cancellationToken));
    }

    [HttpPost("reversals")]
    public async Task<ActionResult<SupplierFinancialReversalWorkflowResult>> Reverse(ReverseSupplierFinancialRequestDto request, CancellationToken cancellationToken)
    {
        if (request.DocumentId <= 0 || request.SourceOperationId == Guid.Empty || string.IsNullOrWhiteSpace(request.DocumentType) || string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest("Supplier financial reversal data is incomplete.");
        var auth = await AuthorizeAsync(Es7Permission.FinanceSupplierReverse, cancellationToken);
        if (auth.Error is not null) return auth.Error;
        return Ok(await coordinator.ReverseAsync(request, auth.User!, CorrelationId, cancellationToken));
    }

    private async Task<(CurrentUserDto? User, ActionResult? Error)> AuthorizeAsync(string permission, CancellationToken cancellationToken)
    {
        var user = await userContext.GetCurrentUserAsync(cancellationToken);
        if (user is null) return (null, Unauthorized());
        return Es7PermissionPolicy.HasPermission(user, permission) ? (user, null) : (null, Forbid());
    }

    private string CorrelationId => ControllerContext.HttpContext?.TraceIdentifier ?? "unbound";
}