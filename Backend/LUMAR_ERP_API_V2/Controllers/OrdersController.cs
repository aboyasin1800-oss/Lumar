using LUMAR_ERP_API_V2.DTOs.Orders;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("orders")]
public sealed class OrdersController(IOrderService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<OrderListDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<OrderListDto>> GetOrders(CancellationToken cancellationToken) => service.GetListAsync(cancellationToken);

    [HttpGet("{id:int}")]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDetailsDto>> GetOrder(int id, CancellationToken cancellationToken)
    {
        var order = await service.GetByIdAsync(id, cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDetailsDto>> CreateOrder(CreateOrderDto order, CancellationToken cancellationToken)
    {
        if (order.DiscountAmount > order.TotalAmount) return BadRequest("Discount cannot exceed total amount.");
        if (order.AdvancePayment > order.TotalAmount - order.DiscountAmount) return BadRequest("Advance payment cannot exceed the amount due.");
        var created = await service.CreateAsync(order, cancellationToken);
        return created is null ? NotFound("Customer does not exist.") : CreatedAtAction(nameof(GetOrder), new { id = created.OrderId }, created);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status405MethodNotAllowed)]
    public ActionResult<OrderDetailsDto> UpdateOrder(int id, UpdateOrderDto order) => StatusCode(StatusCodes.Status405MethodNotAllowed, "Order writes are disabled while LUMAR_ERP is read-only.");

    [HttpGet("{id:int}/items")]
    [ProducesResponseType<IReadOnlyList<OrderItemDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<OrderItemDto>> GetItems(int id, CancellationToken cancellationToken) => service.GetItemsAsync(id, cancellationToken);

    [HttpGet("{id:int}/pieces")]
    [ProducesResponseType<IReadOnlyList<OrderPieceDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<OrderPieceDto>> GetPieces(int id, CancellationToken cancellationToken) => service.GetPiecesAsync(id, cancellationToken);

    [HttpGet("{id:int}/tracking")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public ActionResult<IReadOnlyList<OrderTrackingDto>> GetTracking(int id) => StatusCode(StatusCodes.Status501NotImplemented, "Order tracking is unavailable until the documented TrackingCode type mismatch and missing direct relationship are resolved.");

    [HttpGet("{id:int}/delivery")]
    [ProducesResponseType<OrderDeliveryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDeliveryDto>> GetDelivery(int id, CancellationToken cancellationToken)
    {
        var delivery = await service.GetDeliveryAsync(id, cancellationToken);
        return delivery is null ? NotFound() : Ok(delivery);
    }
}