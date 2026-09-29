using LUMAR_ERP_API_V2.DTOs.Purchasing;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("purchasing/invoices/{invoiceId:int}/lines")]
public sealed class PurchasingInvoiceLinesController(ISupplierInvoiceLineService invoiceLineService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PurchasingInvoiceLineDto>>> Get(int invoiceId, CancellationToken cancellationToken)
    {
        if (invoiceId <= 0) return BadRequest("Supplier invoice id must be positive.");
        var lines = await invoiceLineService.GetForInvoiceAsync(invoiceId, cancellationToken);
        return lines is null ? NotFound() : Ok(lines);
    }
}