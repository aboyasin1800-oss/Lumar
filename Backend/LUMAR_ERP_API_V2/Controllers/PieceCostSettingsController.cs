using LUMAR_ERP_API_V2.DTOs.Pricing;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("piece-cost-settings")]
public sealed class PieceCostSettingsController(IPieceCostSettingService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PieceCostSettingDto>>> GetAll(CancellationToken cancellationToken) => Ok(await service.GetAllAsync(cancellationToken));

    [HttpPut("{productTypeId:int}")]
    public async Task<ActionResult<PieceCostSettingDto>> Upsert(int productTypeId, UpsertPieceCostSettingDto setting, CancellationToken cancellationToken)
    {
        if (productTypeId <= 0) return BadRequest("Product type id must be positive.");
        var saved = await service.UpsertAsync(productTypeId, setting, cancellationToken);
        return saved is null ? NotFound() : Ok(saved);
    }
}