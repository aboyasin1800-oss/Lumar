using LUMAR_ERP_API_V2.DTOs.Production;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("production")]
public sealed class ProductionController(IProductionService service) : ControllerBase
{
    [HttpGet("pieces")]
    public Task<IReadOnlyList<PieceDto>> GetPieces(CancellationToken ct) => service.GetPiecesAsync(ct);

    [HttpGet("pieces/{id:int}")]
    public async Task<ActionResult<PieceDto>> GetPiece(int id, CancellationToken ct) => id <= 0 ? BadRequest("Piece id must be positive.") : await GetPieceResult(id, ct);

    [HttpGet("pieces/{id:int}/work-card")]
    public async Task<ActionResult<WorkCardDto>> GetWorkCard(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest("Piece id must be positive.");
        var card = await service.GetWorkCardAsync(id, ct);
        return card is null ? NotFound() : Ok(card);
    }

    [HttpGet("pieces/{id:int}/tracking")]
    public async Task<ActionResult<IReadOnlyList<TrackingEventDto>>> GetTracking(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest("Piece id must be positive.");
        if (await service.GetPieceByIdAsync(id, ct) is null) return NotFound();
        return Ok(await service.GetPieceTrackingAsync(id, ct));
    }

    [HttpGet("stages")]
    public Task<IReadOnlyList<ProductionStageDto>> GetStages(CancellationToken ct) => service.GetStagesAsync(ct);

    [HttpGet("dashboard")]
    public Task<ProductionDashboardDto> GetDashboard(CancellationToken ct) => service.GetDashboardAsync(ct);

    [HttpGet("readymade-orders")]
    public Task<IReadOnlyList<ReadyMadeProductionOrderDto>> GetReadyMadeOrders(CancellationToken ct) => service.GetReadyMadeOrdersAsync(ct);

    [HttpGet("readymade-orders/{id:int}/items")]
    public Task<IReadOnlyList<ReadyMadeProductionOrderItemDto>> GetReadyMadeOrderItems(int id, CancellationToken ct) => service.GetReadyMadeOrderItemsAsync(id, ct);

    [HttpGet("readymade-order-items/{id:int}/pieces")]
    public Task<IReadOnlyList<ReadyMadeProductionPieceDto>> GetReadyMadeItemPieces(int id, CancellationToken ct) => service.GetReadyMadeItemPiecesAsync(id, ct);

    [HttpGet("deliveries")]
    public Task<IReadOnlyList<ProductionDeliveryDto>> GetDeliveries(CancellationToken ct) => service.GetDeliveriesAsync(ct);

    [HttpPost("pieces")]
    [HttpPut("pieces/{id:int}")]
    [HttpDelete("pieces/{id:int}")]
    [ProducesResponseType(StatusCodes.Status405MethodNotAllowed)]
    public IActionResult WriteDisabled() => StatusCode(StatusCodes.Status405MethodNotAllowed, "Production writes are disabled while LUMAR_ERP is read-only.");

    private async Task<ActionResult<PieceDto>> GetPieceResult(int id, CancellationToken ct)
    {
        var piece = await service.GetPieceByIdAsync(id, ct);
        return piece is null ? NotFound() : Ok(piece);
    }
}