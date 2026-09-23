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
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDetailsDto>> CreateOrder(CreateOrderDto order, CancellationToken cancellationToken)
    {
        try
        {
            if (order.DiscountAmount > order.TotalAmount) return BadRequest("Discount cannot exceed total amount.");
            if (order.AdvancePayment > order.TotalAmount - order.DiscountAmount) return BadRequest("Advance payment cannot exceed the amount due.");
            var created = await service.CreateAsync(order, cancellationToken);
            return created is null ? NotFound("Customer does not exist.") : CreatedAtAction(nameof(GetOrder), new { id = created.OrderId }, created);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
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

    [HttpGet("{id:int}/fabrics")]
    [ProducesResponseType<IReadOnlyList<OrderFabricDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<OrderFabricDto>> GetFabrics(int id, CancellationToken cancellationToken) => service.GetFabricsAsync(id, cancellationToken);

    [HttpGet("{id:int}/payments")]
    [ProducesResponseType<IReadOnlyList<OrderPaymentDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<OrderPaymentDto>> GetPayments(int id, CancellationToken cancellationToken) => service.GetPaymentsAsync(id, cancellationToken);

    [HttpPost("{id:int}/collect")]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDetailsDto>> CollectCustomerPayment(int id, [FromBody] CustomerPaymentRequestDto request, CancellationToken cancellationToken)
    {
        if (id <= 0) return BadRequest("Order id must be positive.");
        if (request is null) return BadRequest("Collection request is required.");
        if (request.Amount <= 0m) return BadRequest("Collection amount must be greater than zero.");

        var order = await service.CollectCustomerPaymentAsync(id, request.Amount, request.PaymentMethod, request.ReferenceNumber, request.Notes, cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost("{id:int}/settle")]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDetailsDto>> SettleCustomerBalance(int id, [FromBody] OrderSettlementRequestDto request, CancellationToken cancellationToken)
    {
        if (id <= 0) return BadRequest("Order id must be positive.");
        if (request is null) return BadRequest("Settlement request is required.");
        if (request.Amount <= 0m && request.DiscountAmount <= 0m) return BadRequest("Collection or discount amount must be greater than zero.");

        var order = await service.SettleCustomerBalanceAsync(id, request.Amount, request.DiscountAmount, request.PaymentMethod, request.ReferenceNumber, request.Notes, cancellationToken);
        return order is null ? NotFound("الطلب غير جاهز للتسوية أو تجاوز المبلغ المتبقي.") : Ok(order);
    }

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

    [HttpPost("{id:int}/delivery/confirm")]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDetailsDto>> Deliver(int id, CancellationToken cancellationToken)
    {
        if (id <= 0) return BadRequest("Order id must be positive.");
        var order = await service.DeliverAsync(id, cancellationToken);
        return order is null
            ? NotFound("الطلب غير موجود أو ليس جاهزًا للتسليم.")
            : Ok(order);
    }

    [HttpPost("{id:int}/delivery/balance-waiver")]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDetailsDto>> WaiveRemainingBalance(int id, CancellationToken cancellationToken)
    {
        if (id <= 0) return BadRequest("Order id must be positive.");
        var order = await service.WaiveRemainingBalanceAsync(id, cancellationToken);
        return order is null
            ? NotFound("الطلب غير جاهز للتبرع بالرصيد أو لا يحتوي على رصيد متبقٍ.")
            : Ok(order);
    }

    [HttpPost("{id:int}/delivery/revenue-recognize")]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDetailsDto>> RecognizeDeliveryRevenue(int id, CancellationToken cancellationToken)
    {
        var order = await service.RecognizeDeliveryRevenueAsync(id, cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDetailsDto>> CancelOrder(int id, [FromBody] CancelOrderRequestDto request, CancellationToken cancellationToken)
    {
        var order = await service.CancelOrderAsync(id, request?.Reason, request?.CancelledBy, cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }
}