using LUMAR_ERP_API_V2.Authorization;
using LUMAR_ERP_API_V2.DTOs.Purchasing;
using LUMAR_ERP_API_V2.DTOs.Suppliers;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("suppliers")]
public sealed class SuppliersController(
    ISupplierService service,
    IAuthenticatedUserContext userContext,
    ISupplierOwnershipResolver supplierOwnershipResolver,
    Es7OperationalAudit audit,
    Es7OperationalTestMode? testMode = null) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SupplierListDto>>> All(CancellationToken cancellationToken) =>
        Ok(await service.GetAllAsync(cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SupplierDetailsDto>> One(int id, CancellationToken cancellationToken)
    {
        var result = await Parent(id, cancellationToken);
        return result.Error ?? Ok(result.Value!);
    }

    [HttpPost]
    public async Task<ActionResult<SupplierDetailsDto>> Create(CreateSupplierRequestDto request, CancellationToken cancellationToken)
    {
        if (request.SourceOperationId == Guid.Empty || string.IsNullOrWhiteSpace(request.SupplierCode) || string.IsNullOrWhiteSpace(request.SupplierName))
            return BadRequest("بيانات المورد غير مكتملة.");
        var user = await userContext.GetCurrentUserAsync(cancellationToken);
        if (user is null) return Unauthorized();
        if (!Es7OperationalAuthorization.HasPermission(user, Es7Permission.PurchasingManage, testMode, ControllerContext.HttpContext?.Request))
            return StatusCode(StatusCodes.Status403Forbidden);
        try
        {
            var created = await service.CreateAsync(request, cancellationToken);
            audit.Record(user, "SupplierCreated", request.SourceOperationId, created.SupplierId, ControllerContext.HttpContext?.TraceIdentifier ?? "unbound");
            return CreatedAtAction(nameof(One), new { id = created.SupplierId }, created);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
        catch (Microsoft.Data.SqlClient.SqlException exception) when (exception.Number is 2601 or 2627)
        {
            return Conflict("يوجد مورد مسجل بالرمز نفسه.");
        }
    }

    [HttpGet("{id:int}/transactions")]
    public async Task<ActionResult<IReadOnlyList<SupplierTransactionDto>>> Transactions(int id, CancellationToken cancellationToken)
    {
        var result = await Parent(id, cancellationToken);
        return result.Error ?? Ok(await service.GetTransactionsAsync(id, cancellationToken));
    }

    [HttpGet("{id:int}/ledger")]
    public async Task<ActionResult<IReadOnlyList<SupplierLedgerEntryDto>>> Ledger(int id, CancellationToken cancellationToken)
    {
        var result = await Parent(id, cancellationToken);
        return result.Error ?? Ok(await service.GetLedgerAsync(id, cancellationToken));
    }

    [HttpGet("{id:int}/invoices")]
    public async Task<ActionResult<IReadOnlyList<SupplierInvoiceDto>>> Invoices(int id, CancellationToken cancellationToken)
    {
        var result = await Parent(id, cancellationToken);
        return result.Error ?? Ok(await service.GetInvoicesAsync(id, cancellationToken));
    }

    [HttpGet("{id:int}/payments")]
    public async Task<ActionResult<IReadOnlyList<SupplierPaymentDto>>> Payments(int id, CancellationToken cancellationToken)
    {
        var result = await Parent(id, cancellationToken);
        return result.Error ?? Ok(await service.GetPaymentsAsync(id, cancellationToken));
    }

    [HttpGet("{id:int}/payment-allocations")]
    public async Task<ActionResult<IReadOnlyList<SupplierPaymentAllocationDto>>> Allocations(int id, CancellationToken cancellationToken)
    {
        var result = await Parent(id, cancellationToken);
        return result.Error ?? Ok(await service.GetAllocationsAsync(id, cancellationToken));
    }

    [HttpGet("/mobile/supplier/profile")]
    public async Task<ActionResult<SupplierDetailsDto>> MobileProfile(CancellationToken cancellationToken)
    {
        var auth = await ResolveAuthenticatedSupplierAsync(cancellationToken);
        if (auth.Error is not null)
            return auth.Error;

        var supplier = await service.GetByIdAsync(auth.SupplierId!.Value, cancellationToken);
        return supplier is null ? NotFound() : Ok(supplier);
    }

    [HttpGet("/mobile/supplier/home")]
    public async Task<ActionResult<SupplierHomeSummaryDto>> MobileHome(CancellationToken cancellationToken)
    {
        var auth = await ResolveAuthenticatedSupplierAsync(cancellationToken);
        if (auth.Error is not null)
            return auth.Error;

        var summary = await service.GetHomeSummaryAsync(auth.SupplierId!.Value, cancellationToken);
        return summary is null ? NotFound() : Ok(summary);
    }

    [HttpGet("/mobile/supplier/ledger")]
    public async Task<ActionResult<IReadOnlyList<SupplierLedgerEntryDto>>> MobileLedger(CancellationToken cancellationToken)
    {
        var auth = await ResolveAuthenticatedSupplierAsync(cancellationToken);
        if (auth.Error is not null)
            return auth.Error;

        return Ok(await service.GetLedgerAsync(auth.SupplierId!.Value, cancellationToken));
    }

    [HttpGet("/mobile/supplier/invoices")]
    public async Task<ActionResult<IReadOnlyList<SupplierInvoiceDto>>> MobileInvoices(CancellationToken cancellationToken)
    {
        var auth = await ResolveAuthenticatedSupplierAsync(cancellationToken);
        if (auth.Error is not null)
            return auth.Error;

        return Ok(await service.GetInvoicesAsync(auth.SupplierId!.Value, cancellationToken));
    }

    [HttpGet("/mobile/supplier/invoices/{invoiceId:int}")]
    public async Task<ActionResult<SupplierInvoiceDto>> MobileInvoice(int invoiceId, CancellationToken cancellationToken)
    {
        var auth = await ResolveAuthenticatedSupplierAsync(cancellationToken);
        if (auth.Error is not null)
            return auth.Error;

        if (invoiceId <= 0)
            return BadRequest("Invoice id must be positive.");

        var invoice = await service.GetInvoiceAsync(auth.SupplierId!.Value, invoiceId, cancellationToken);
        return invoice is null ? NotFound() : Ok(invoice);
    }

    [HttpGet("/mobile/supplier/payments")]
    public async Task<ActionResult<IReadOnlyList<SupplierPaymentDto>>> MobilePayments(CancellationToken cancellationToken)
    {
        var auth = await ResolveAuthenticatedSupplierAsync(cancellationToken);
        if (auth.Error is not null)
            return auth.Error;

        return Ok(await service.GetPaymentsAsync(auth.SupplierId!.Value, cancellationToken));
    }

    [HttpGet("/mobile/supplier/payments/{paymentId:int}")]
    public async Task<ActionResult<SupplierPaymentDto>> MobilePayment(int paymentId, CancellationToken cancellationToken)
    {
        var auth = await ResolveAuthenticatedSupplierAsync(cancellationToken);
        if (auth.Error is not null)
            return auth.Error;

        if (paymentId <= 0)
            return BadRequest("Payment id must be positive.");

        var payment = await service.GetPaymentAsync(auth.SupplierId!.Value, paymentId, cancellationToken);
        return payment is null ? NotFound() : Ok(payment);
    }

    [HttpGet("/mobile/supplier/payments/{paymentId:int}/response")]
    public async Task<ActionResult<SupplierPaymentResponseStatusDto>> MobilePaymentResponse(int paymentId, CancellationToken cancellationToken)
    {
        var auth = await ResolveAuthenticatedSupplierAsync(cancellationToken);
        if (auth.Error is not null)
            return auth.Error;

        if (paymentId <= 0)
            return BadRequest("Payment id must be positive.");

        var payment = await service.GetPaymentAsync(auth.SupplierId!.Value, paymentId, cancellationToken);
        if (payment is null)
            return NotFound();

        var status = await service.GetPaymentResponseStatusAsync(auth.SupplierId!.Value, paymentId, cancellationToken);
        return status is null
            ? Ok(new SupplierPaymentResponseStatusDto(paymentId, auth.SupplierId!.Value, payment.PaymentNumber, "NotAcknowledged", "NoDispute", null, null, null, payment.PaymentDate))
            : Ok(status);
    }

    [HttpGet("/mobile/supplier/goods-receipts")]
    public async Task<ActionResult<IReadOnlyList<GoodsReceiptDto>>> MobileGoodsReceipts(CancellationToken cancellationToken)
    {
        var auth = await ResolveAuthenticatedSupplierAsync(cancellationToken);
        if (auth.Error is not null)
            return auth.Error;

        return Ok(await service.GetGoodsReceiptsAsync(auth.SupplierId!.Value, cancellationToken));
    }

    [HttpGet("/mobile/supplier/goods-receipts/{receiptId:int}")]
    public async Task<ActionResult<GoodsReceiptDto>> MobileGoodsReceipt(int receiptId, CancellationToken cancellationToken)
    {
        var auth = await ResolveAuthenticatedSupplierAsync(cancellationToken);
        if (auth.Error is not null)
            return auth.Error;

        if (receiptId <= 0)
            return BadRequest("Goods receipt id must be positive.");

        var receipt = await service.GetGoodsReceiptAsync(auth.SupplierId!.Value, receiptId, cancellationToken);
        return receipt is null ? NotFound() : Ok(receipt);
    }

    [HttpPut("{id:int}")]
    [HttpDelete("{id:int}")]
    public IActionResult Disabled() => StatusCode(405, "Supplier update and delete operations are disabled.");

    private async Task<(SupplierDetailsDto? Value, ActionResult? Error)> Parent(int id, CancellationToken cancellationToken)
    {
        if (id <= 0) return (null, BadRequest("Supplier id must be positive."));
        var supplier = await service.GetByIdAsync(id, cancellationToken);
        return supplier is null ? (null, NotFound()) : (supplier, null);
    }

    private async Task<(int? SupplierId, ActionResult? Error)> ResolveAuthenticatedSupplierAsync(CancellationToken cancellationToken)
    {
        var user = await userContext.GetCurrentUserAsync(cancellationToken);
        if (user is null)
            return (null, Unauthorized());

        var supplierId = await supplierOwnershipResolver.ResolveCurrentSupplierAsync(cancellationToken);
        if (!supplierId.HasValue)
            return (null, StatusCode(StatusCodes.Status403Forbidden, "Supplier mobile access is restricted to authenticated supplier accounts."));

        return (supplierId.Value, null);
    }
}