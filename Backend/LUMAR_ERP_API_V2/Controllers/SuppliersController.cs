using LUMAR_ERP_API_V2.Authorization;
using LUMAR_ERP_API_V2.DTOs.Suppliers;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("suppliers")]
public sealed class SuppliersController(
    ISupplierService service,
    IAuthenticatedUserContext userContext,
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
            return Forbid();
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

    [HttpPut("{id:int}")]
    [HttpDelete("{id:int}")]
    public IActionResult Disabled() => StatusCode(405, "Supplier update and delete operations are disabled.");

    private async Task<(SupplierDetailsDto? Value, ActionResult? Error)> Parent(int id, CancellationToken cancellationToken)
    {
        if (id <= 0) return (null, BadRequest("Supplier id must be positive."));
        var supplier = await service.GetByIdAsync(id, cancellationToken);
        return supplier is null ? (null, NotFound()) : (supplier, null);
    }
}