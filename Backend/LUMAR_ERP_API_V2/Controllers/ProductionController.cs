using LUMAR_ERP_API_V2.DTOs.Production;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("production")]
public sealed class ProductionController(IProductionService service, IProductionTrackingService productionTrackingService) : ControllerBase
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

    [HttpGet("factory-monitoring")]
    public Task<FactoryMonitoringDashboardDto> GetFactoryMonitoring(CancellationToken ct) => service.GetFactoryMonitoringAsync(ct);

    [HttpGet("readymade-orders")]
    public Task<IReadOnlyList<ReadyMadeProductionOrderDto>> GetReadyMadeOrders(CancellationToken ct) => service.GetReadyMadeOrdersAsync(ct);

    [HttpGet("readymade-orders/{id:int}")]
    public async Task<ActionResult<ReadyMadeProductionOrderDto>> GetReadyMadeOrder(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest("Order id must be positive.");
        var order = await service.GetReadyMadeOrderByIdAsync(id, ct);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpGet("readymade-orders/{id:int}/items")]
    public Task<IReadOnlyList<ReadyMadeProductionOrderItemDto>> GetReadyMadeOrderItems(int id, CancellationToken ct) => service.GetReadyMadeOrderItemsAsync(id, ct);

    [HttpPost("readymade-orders")]
    [ProducesResponseType<ReadyMadeProductionOrderCreateResultDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReadyMadeProductionOrderCreateResultDto>> CreateReadyMadeOrder(ReadyMadeProductionOrderCreateDto order, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(order.ProductionName)) return BadRequest("اسم المنتج مطلوب.");
        if (order.Items == null || order.Items.Count == 0) return BadRequest("يجب إضافة بند واحد على الأقل.");
        if (order.Items.Any(item => item.ProductTypeId <= 0)) return BadRequest("ProductTypeId الرسمي مطلوب لكل بند إنتاج جاهز.");
        if (order.TotalCost < 0) return BadRequest("التكلفة غير صالحة.");
        if (order.ProfitPercentage < 0) return BadRequest("نسبة الربح غير صالحة.");
        if (order.SuggestedSellingPrice < 0) return BadRequest("سعر البيع المقترح غير صالح.");

        var created = await service.CreateReadyMadeOrderAsync(order, ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpGet("readymade-order-items/{id:int}/pieces")]
    public Task<IReadOnlyList<ReadyMadeProductionPieceDto>> GetReadyMadeItemPieces(int id, CancellationToken ct) => service.GetReadyMadeItemPiecesAsync(id, ct);

    [HttpGet("readymade-pieces/{id:int}/tracking")]
    public Task<IReadOnlyList<TrackingEventDto>> GetReadyMadePieceTracking(int id, CancellationToken ct) => service.GetReadyMadePieceTrackingAsync(id, ct);

    [HttpGet("deliveries")]
    public Task<IReadOnlyList<ProductionDeliveryDto>> GetDeliveries(CancellationToken ct) => service.GetDeliveriesAsync(ct);

    [HttpGet("routes/{pieceType}")]
    public ActionResult<IReadOnlyList<string>> GetRoute(string pieceType)
    {
        var route = productionTrackingService.GetRoute(pieceType);
        return route.Count == 0 ? NotFound() : Ok(route);
    }

    [HttpGet("pieces/route")]
    public async Task<ActionResult<ProductionTrackingRouteDto>> GetPieceRoute([FromQuery] int? pieceId, [FromQuery] string? trackingCode, CancellationToken ct)
    {
        if (pieceId is null && string.IsNullOrWhiteSpace(trackingCode))
            return BadRequest("Either pieceId or trackingCode is required.");

        ProductionTrackingRouteDto? route;
        if (pieceId.HasValue && pieceId.Value > 0)
        {
            route = await service.GetPieceRouteAsync(pieceId.Value, ct);
        }
        else
        {
            route = await service.GetPieceRouteByTrackingCodeAsync(trackingCode!, ct);
        }

        return route is null ? NotFound() : Ok(route);
    }

    [HttpPost("pieces/advance")]
    [ProducesResponseType<ProductionTrackingAdvanceResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductionTrackingAdvanceResultDto>> AdvancePiece([FromBody] ProductionTrackingAdvanceRequestDto request, CancellationToken ct)
    {
        if (request is null) return BadRequest("Request body is required.");
        if (request.PieceId is null && string.IsNullOrWhiteSpace(request.TrackingCode)) return BadRequest("Either PieceId or TrackingCode is required.");
        if (string.IsNullOrWhiteSpace(request.RequestedStage)) return BadRequest("Requested stage is required.");
        if (request.ProductTypeId <= 0) return BadRequest("ProductTypeId is required.");

        ProductionTrackingRouteDto? route = null;
        if (!string.IsNullOrWhiteSpace(request.TrackingCode))
        {
            route = await service.GetPieceRouteByTrackingCodeAsync(request.TrackingCode, ct);
        }
        else if (request.PieceId is > 0)
        {
            route = await service.GetPieceRouteAsync(request.PieceId.Value, ct);
        }

        var resolvedRequest = request with
        {
            PieceType = string.IsNullOrWhiteSpace(request.PieceType) ? route?.PieceType ?? string.Empty : request.PieceType.Trim(),
        };

        if (route is not null && route.ProductTypeId != request.ProductTypeId)
            return BadRequest("ProductTypeId does not match the piece's official product type.");

        if (string.IsNullOrWhiteSpace(resolvedRequest.PieceType))
            return BadRequest("Piece type could not be resolved for the supplied piece information.");

        var result = await service.AdvancePieceStageAsync(resolvedRequest, ct);
        if (result is null) return NotFound();
        if (!result.Updated) return BadRequest(result);
        return Ok(result);
    }

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