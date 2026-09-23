using LUMAR_ERP_API_V2.DTOs.Loyalty;
using LUMAR_ERP_API_V2.Services;
using LUMAR_ERP_API_V2.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("api/piece-points")]
public sealed class PiecePointsController(IPiecePointSettingsService service) : ControllerBase
{
    [HttpGet("product-settings")]
    public async Task<ActionResult<IReadOnlyList<ProductLoyaltyPointSettingDto>>> GetProductSettings(CancellationToken cancellationToken)
        => Ok(await service.GetProductLoyaltyPointSettingsAsync(cancellationToken));

    [HttpPut("product-settings/{productTypeId:int}")]
    public async Task<ActionResult<ProductLoyaltyPointSettingDto>> UpdateProductSetting(int productTypeId, [FromBody] UpdateProductLoyaltyPointSettingDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.UpsertProductLoyaltyPointSettingAsync(productTypeId, request, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpGet("ready-made-product-settings")]
    public async Task<ActionResult<IReadOnlyList<ReadyMadeProductTypeLoyaltyPointSettingDto>>> GetReadyMadeProductSettings(CancellationToken cancellationToken)
        => Ok(await service.GetReadyMadeProductTypeLoyaltyPointSettingsAsync(cancellationToken));

    [HttpPut("ready-made-product-settings/{productTypeId:int}")]
    public async Task<ActionResult<ReadyMadeProductTypeLoyaltyPointSettingDto>> UpdateReadyMadeProductSetting(int productTypeId, [FromBody] UpdateReadyMadeProductTypeLoyaltyPointSettingDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.UpsertReadyMadeProductTypeLoyaltyPointSettingAsync(productTypeId, request, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpGet("imported-product-settings")]
    public async Task<ActionResult<IReadOnlyList<ImportedProductLoyaltyPointSettingDto>>> GetImportedProductSettings(CancellationToken cancellationToken)
        => Ok(await service.GetImportedProductLoyaltyPointSettingsAsync(cancellationToken));

    [HttpPut("imported-product-settings/{importedReadyMadeProductId:int}")]
    public async Task<ActionResult<ImportedProductLoyaltyPointSettingDto>> UpdateImportedProductSetting(int importedReadyMadeProductId, [FromBody] UpdateImportedProductLoyaltyPointSettingDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.UpsertImportedProductLoyaltyPointSettingAsync(importedReadyMadeProductId, request, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpGet("settings/{pieceCode}")]
    public async Task<ActionResult<LoyaltyPiecePointSettingDto>> GetSetting(string pieceCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(pieceCode)) return BadRequest("PieceCode is required.");
        var setting = await service.GetActivePieceSettingAsync(pieceCode, cancellationToken);
        return setting is null ? NotFound() : Ok(setting);
    }

    [HttpGet("program-settings")]
    public async Task<ActionResult<LoyaltyProgramSettingsDto>> GetProgramSettings(CancellationToken cancellationToken)
    {
        var settings = await service.GetProgramSettingsAsync(cancellationToken);
        return settings is null ? NotFound() : Ok(settings);
    }

    [HttpPost("evaluate")]
    public async Task<ActionResult<PiecePointsResultDto>> Evaluate([FromBody] PiecePointCalculationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PieceCode)) return BadRequest("PieceCode is required.");
        if (request.Quantity <= 0) return BadRequest("Quantity must be greater than zero.");

        var result = await service.EvaluatePiecePointsAsync(1, 999, request.PieceCode, request.Quantity, request.Source ?? "PiecePurchase", request.Notes, cancellationToken);
        return Ok(result);
    }

    [HttpPost("evaluate-official")]
    public async Task<ActionResult<PiecePointsResultDto>> EvaluateOfficial([FromBody] OfficialPiecePointCalculationRequest request, CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0) return BadRequest("Quantity must be greater than zero.");
        if ((request.ProductTypeId is null) == (request.ImportedReadyMadeProductId is null))
            return BadRequest("Provide exactly one official source id.");

        var setting = request.ProductTypeId is int productTypeId
            ? await service.GetActiveProductPointSettingAsync(productTypeId, cancellationToken)
            : await service.GetActiveImportedPointSettingAsync(request.ImportedReadyMadeProductId!.Value, cancellationToken);
        var program = await service.GetProgramSettingsAsync(cancellationToken);
        var basePoints = PiecePointsEngine.CalculateForItem(
            setting,
            request.Quantity,
            0m,
            program?.IsEnabled == true);
        var buyerEarn = PiecePointsEngine.CalculateBuyerEarn(basePoints, "official-source", request.Quantity, 0m);
        return Ok(new PiecePointsResultDto(
            basePoints,
            buyerEarn,
            ReferralRewardEngine.CalculateLevels(basePoints, 1, 2, 3, 4),
            "official-evaluation",
            $"{request.ProductTypeId}:{request.ImportedReadyMadeProductId}:{request.Quantity}"));
    }
}
