using LUMAR_ERP_API_V2.DTOs.Pricing;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("piece-cost-management")]
public sealed class PieceCostManagementController(IPieceCostManagementService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PieceCostManagementDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await service.GetAllAsync(cancellationToken));

    [HttpPut("{productTypeId:int}")]
    public async Task<ActionResult<PieceCostManagementDto>> Update(
        int productTypeId,
        [FromBody] UpdatePieceCostManagementDto request,
        CancellationToken cancellationToken)
    {
        if (productTypeId <= 0)
        {
            return BadRequest(new { message = "معرف نوع القطعة غير صالح." });
        }

        var updated = await service.UpdateAsync(productTypeId, request, cancellationToken);
        return updated is null
            ? NotFound(new { message = "نوع القطعة غير موجود أو غير نشط." })
            : Ok(updated);
    }
}