using LUMAR_ERP_API_V2.Authorization;
using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.DTOs.Orders;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("orders")]
public sealed class OrdersController(IOrderService service, IAuthenticatedUserContext? userContext = null) : ControllerBase
{
    private async Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken)
        => userContext is null ? null : await userContext.GetCurrentUserAsync(cancellationToken);

    private static ActionResult? EnforceCustomerAccess(CurrentUserDto? user, int? orderCustomerId)
    {
        if (user is null)
            return new UnauthorizedResult();

        if (!CustomerOrderOwnershipGuard.IsCustomerAccount(user))
            return null;

        return CustomerOrderOwnershipGuard.IsAllowed(user, orderCustomerId)
            ? null
            : new NotFoundResult();
    }

    private static ActionResult? EnforceCustomerAccess(CurrentUserDto? user, int orderCustomerId)
        => EnforceCustomerAccess(user, (int?)orderCustomerId);

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<OrderListDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrderListDto>>> GetOrders(CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null)
            return Unauthorized();

        var orders = await service.GetListAsync(cancellationToken);
        if (!CustomerOrderOwnershipGuard.IsCustomerAccount(user))
            return Ok(orders);

        var customerId = user.CustomerId!.Value;
        return Ok(orders.Where(order => order.CustomerId == customerId).ToList());
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDetailsDto>> GetOrder(int id, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null)
            return Unauthorized();

        var order = await service.GetByIdAsync(id, cancellationToken);
        if (order is null)
            return NotFound();

        var access = EnforceCustomerAccess(user, order.CustomerId);
        if (access is not null)
            return access;

        return Ok(order);
    }

    [HttpPost]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDetailsDto>> CreateOrder(CreateOrderDto order, CancellationToken cancellationToken)
    {
        try
        {
            var user = await GetCurrentUserAsync(cancellationToken);
            if (user is null)
                return Unauthorized();

            var safeOrder = order;
            if (CustomerOrderOwnershipGuard.IsCustomerAccount(user))
            {
                safeOrder = new CreateOrderDto
                {
                    CustomerId = user.CustomerId!.Value,
                    OrderDate = order.OrderDate,
                    DeliveryDate = order.DeliveryDate,
                    TotalAmount = order.TotalAmount,
                    DiscountAmount = order.DiscountAmount,
                    AdvancePayment = order.AdvancePayment,
                    UrgencyStatus = order.UrgencyStatus,
                    Notes = order.Notes,
                    SaleCategory = order.SaleCategory,
                    PaymentMethod = order.PaymentMethod,
                    CashAccountId = order.CashAccountId,
                    RequestReference = order.RequestReference,
                    Items = order.Items
                };
            }

            if (safeOrder.DiscountAmount > safeOrder.TotalAmount) return BadRequest("Discount cannot exceed total amount.");
            if (safeOrder.AdvancePayment > safeOrder.TotalAmount - safeOrder.DiscountAmount) return BadRequest("Advance payment cannot exceed the amount due.");
            var created = await service.CreateAsync(safeOrder, cancellationToken);
            return created is null ? NotFound("Customer does not exist.") : CreatedAtAction(nameof(GetOrder), new { id = created.OrderId }, created);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status405MethodNotAllowed)]
    public ActionResult<OrderDetailsDto> UpdateOrder(int id, UpdateOrderDto order) => StatusCode(StatusCodes.Status405MethodNotAllowed, "Order writes are disabled while LUMAR_ERP is read-only.");

    [HttpGet("{id:int}/items")]
    [ProducesResponseType<IReadOnlyList<OrderItemDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrderItemDto>>> GetItems(int id, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null)
            return Unauthorized();

        var order = await service.GetByIdAsync(id, cancellationToken);
        if (order is null)
            return NotFound();

        var access = EnforceCustomerAccess(user, order.CustomerId);
        if (access is not null)
            return access;

        return Ok(await service.GetItemsAsync(id, cancellationToken));
    }

    [HttpGet("{id:int}/pieces")]
    [ProducesResponseType<IReadOnlyList<OrderPieceDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrderPieceDto>>> GetPieces(int id, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null)
            return Unauthorized();

        var order = await service.GetByIdAsync(id, cancellationToken);
        if (order is null)
            return NotFound();

        var access = EnforceCustomerAccess(user, order.CustomerId);
        if (access is not null)
            return access;

        return Ok(await service.GetPiecesAsync(id, cancellationToken));
    }

    [HttpGet("{id:int}/fabrics")]
    [ProducesResponseType<IReadOnlyList<OrderFabricDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrderFabricDto>>> GetFabrics(int id, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null)
            return Unauthorized();

        var order = await service.GetByIdAsync(id, cancellationToken);
        if (order is null)
            return NotFound();

        var access = EnforceCustomerAccess(user, order.CustomerId);
        if (access is not null)
            return access;

        return Ok(await service.GetFabricsAsync(id, cancellationToken));
    }

    [HttpGet("{id:int}/payments")]
    [ProducesResponseType<IReadOnlyList<OrderPaymentDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrderPaymentDto>>> GetPayments(int id, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null)
            return Unauthorized();

        var order = await service.GetByIdAsync(id, cancellationToken);
        if (order is null)
            return NotFound();

        var access = EnforceCustomerAccess(user, order.CustomerId);
        if (access is not null)
            return access;

        return Ok(await service.GetPaymentsAsync(id, cancellationToken));
    }

    [HttpPost("{id:int}/collect")]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDetailsDto>> CollectCustomerPayment(int id, [FromBody] CustomerPaymentRequestDto request, CancellationToken cancellationToken)
    {
        if (id <= 0) return BadRequest("Order id must be positive.");
        if (request is null) return BadRequest("Collection request is required.");
        if (request.Amount <= 0m) return BadRequest("Collection amount must be greater than zero.");

        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null)
            return Unauthorized();

        var order = await service.GetByIdAsync(id, cancellationToken);
        if (order is null)
            return NotFound();

        var access = EnforceCustomerAccess(user, order.CustomerId);
        if (access is not null)
            return access;

        var result = await service.CollectCustomerPaymentAsync(id, request.Amount, request.PaymentMethod, request.CashAccountId, request.ReferenceNumber, request.Notes, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:int}/settle")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDetailsDto>> SettleCustomerBalance(int id, [FromBody] OrderSettlementRequestDto request, CancellationToken cancellationToken)
    {
        if (id <= 0) return BadRequest("Order id must be positive.");
        if (request is null) return BadRequest("Settlement request is required.");
        if (request.Amount <= 0m && request.DiscountAmount <= 0m) return BadRequest("Collection or discount amount must be greater than zero.");

        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null)
            return Unauthorized();

        var order = await service.GetByIdAsync(id, cancellationToken);
        if (order is null)
            return NotFound();

        var access = EnforceCustomerAccess(user, order.CustomerId);
        if (access is not null)
            return access;

        var result = await service.SettleCustomerBalanceAsync(id, request.Amount, request.DiscountAmount, request.PaymentMethod, request.CashAccountId, request.ReferenceNumber, request.Notes, cancellationToken);
        return result is null ? NotFound("الطلب غير جاهز للتسوية أو تجاوز المبلغ المتبقي.") : Ok(result);
    }

    [HttpGet("{id:int}/tracking")]
    [ProducesResponseType<IReadOnlyList<OrderTrackingDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<OrderTrackingDto>>> GetTracking(int id, CancellationToken cancellationToken)
    {
        if (id <= 0) return BadRequest("Order id must be positive.");

        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null)
            return Unauthorized();

        var order = await service.GetByIdAsync(id, cancellationToken);
        if (order is null)
            return NotFound();

        var access = EnforceCustomerAccess(user, order.CustomerId);
        if (access is not null)
            return access;

        var tracking = await service.GetTrackingAsync(id, cancellationToken);
        return tracking is null ? NotFound() : Ok(tracking);
    }

    [HttpGet("{id:int}/delivery")]
    [ProducesResponseType<OrderDeliveryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDeliveryDto>> GetDelivery(int id, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null)
            return Unauthorized();

        var order = await service.GetByIdAsync(id, cancellationToken);
        if (order is null)
            return NotFound();

        var access = EnforceCustomerAccess(user, order.CustomerId);
        if (access is not null)
            return access;

        var delivery = await service.GetDeliveryAsync(id, cancellationToken);
        return delivery is null ? NotFound() : Ok(delivery);
    }

    [HttpPost("{id:int}/delivery/confirm")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDetailsDto>> Deliver(int id, CancellationToken cancellationToken)
    {
        if (id <= 0) return BadRequest("Order id must be positive.");

        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null)
            return Unauthorized();

        var order = await service.GetByIdAsync(id, cancellationToken);
        if (order is null)
            return NotFound();

        var access = EnforceCustomerAccess(user, order.CustomerId);
        if (access is not null)
            return access;

        var result = await service.DeliverAsync(id, cancellationToken);
        return result is null ? NotFound("الطلب غير موجود أو ليس جاهزًا للتسليم.") : Ok(result);
    }

    [HttpPost("{id:int}/delivery/balance-waiver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDetailsDto>> WaiveRemainingBalance(int id, CancellationToken cancellationToken)
    {
        if (id <= 0) return BadRequest("Order id must be positive.");

        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null)
            return Unauthorized();

        var order = await service.GetByIdAsync(id, cancellationToken);
        if (order is null)
            return NotFound();

        var access = EnforceCustomerAccess(user, order.CustomerId);
        if (access is not null)
            return access;

        var result = await service.WaiveRemainingBalanceAsync(id, cancellationToken);
        return result is null ? NotFound("الطلب غير جاهز للتبرع بالرصيد أو لا يحتوي على رصيد متبقٍ.") : Ok(result);
    }

    [HttpPost("{id:int}/delivery/revenue-recognize")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDetailsDto>> RecognizeDeliveryRevenue(int id, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null)
            return Unauthorized();

        var order = await service.GetByIdAsync(id, cancellationToken);
        if (order is null)
            return NotFound();

        var access = EnforceCustomerAccess(user, order.CustomerId);
        if (access is not null)
            return access;

        var result = await service.RecognizeDeliveryRevenueAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDetailsDto>> CancelOrder(int id, [FromBody] CancelOrderRequestDto request, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null)
            return Unauthorized();

        var order = await service.GetByIdAsync(id, cancellationToken);
        if (order is null)
            return NotFound();

        var access = EnforceCustomerAccess(user, order.CustomerId);
        if (access is not null)
            return access;

        var result = await service.CancelOrderAsync(id, request?.Reason, request?.CancelledBy, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}