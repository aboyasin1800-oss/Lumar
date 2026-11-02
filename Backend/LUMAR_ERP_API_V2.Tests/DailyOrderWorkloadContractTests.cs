using LUMAR_ERP_API_V2.Authorization;
using LUMAR_ERP_API_V2.Controllers;
using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.DTOs.Orders;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class DailyOrderWorkloadContractTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(24, false)]
    [InlineData(25, true)]
    [InlineData(26, true)]
    public void Daily_order_count_boundaries_are_enforced(int dailyOrderCount, bool expectedHigh)
    {
        var result = OrderService.EvaluateDailyOrderWorkload(dailyOrderCount);

        Assert.Equal(expectedHigh, result.IsWorkloadHigh);
        Assert.Equal(expectedHigh ? "نعتذر عن أي تأخير محتمل. يوجد ضغط في الطلبات حالياً، ويعمل فريقنا على إنجاز طلبكم بأسرع وقت ممكن." : null, result.Message);
    }

    [Fact]
    public void Public_contract_does_not_expose_daily_order_count()
    {
        var properties = typeof(DailyOrderWorkloadStatusDto).GetProperties();

        Assert.DoesNotContain(properties, property => property.Name.Contains("Count", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, properties.Length);
        Assert.Contains(properties, property => property.Name == nameof(DailyOrderWorkloadStatusDto.IsWorkloadHigh));
        Assert.Contains(properties, property => property.Name == nameof(DailyOrderWorkloadStatusDto.Message));
    }

    [Fact]
    public async Task Unauthenticated_request_is_rejected()
    {
        var controller = new OrdersController(new StubOrderService(), new FakeUserContext(null));

        var result = await controller.GetDailyWorkloadStatus(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task Authenticated_customer_request_returns_safe_contract()
    {
        var controller = new OrdersController(new StubOrderService(), new FakeUserContext(new CurrentUserDto(8, "customer01", "Customer One", "Customer", true, DateTime.UtcNow.AddDays(-1), "Customer", 101, null, null)));

        var result = await controller.GetDailyWorkloadStatus(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<DailyOrderWorkloadStatusDto>(ok.Value);
        Assert.True(dto.IsWorkloadHigh);
        Assert.Equal("نعتذر عن أي تأخير محتمل. يوجد ضغط في الطلبات حالياً، ويعمل فريقنا على إنجاز طلبكم بأسرع وقت ممكن.", dto.Message);
    }

    [Fact]
    public async Task Tailoring_edit_requires_authenticated_user()
    {
        var controller = new OrdersController(new StubTailoringOrderService(), new FakeUserContext(null));

        var response = await controller.ApplyTailoringEdit(42, new TailoringOrderEditRequest
        {
            EditOperationId = Guid.NewGuid(),
            ExpectedVersion = "v1",
            AddedPieces = [new TailoringNewPiece { AddOperationId = Guid.NewGuid(), ProductTypeId = 1, FabricCode = "FAB-001", Measurements = new Dictionary<string, string> { ["Length"] = "36" } }]
        }, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response.Result);
    }

    [Fact]
    public async Task Tailoring_edit_rejects_cross_customer_access_and_maps_conflicts_to_http_409()
    {
        var customer = new CurrentUserDto(9, "cust02", "Customer Two", "Customer", true, DateTime.UtcNow.AddDays(-1), "Customer", 9, null, null);
        var service = new StubTailoringOrderService(new OrderDetailsDto(42, "ORD-42", 101, "Customer One", "0999999999", DateTime.UtcNow, null, 500m, 0m, 500m, 0m, "Normal", "New", null, DateTime.UtcNow, null, null, null, null, "TailoringOrder", false, null, false, null, null));
        var controller = new OrdersController(service, new FakeUserContext(customer));

        var forbidden = await controller.GetTailoringEdit(42, CancellationToken.None);
        var forbiddenResult = Assert.IsType<ObjectResult>(forbidden.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbiddenResult.StatusCode);

        var conflict = await controller.ApplyTailoringEdit(42, new TailoringOrderEditRequest
        {
            EditOperationId = Guid.NewGuid(),
            ExpectedVersion = "v1",
            AddedPieces = [new TailoringNewPiece { AddOperationId = Guid.NewGuid(), ProductTypeId = 1, FabricCode = "FAB-001", Measurements = new Dictionary<string, string> { ["Length"] = "36" } }]
        }, CancellationToken.None);

        var httpConflict = Assert.IsType<ObjectResult>(conflict.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, httpConflict.StatusCode);
        Assert.NotNull(httpConflict.Value);
        Assert.Contains("صلاحية", httpConflict.Value.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubOrderService : IOrderService
    {
        public Task<IReadOnlyList<OrderListDto>> GetListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderListDto>>([]);
        public Task<OrderDetailsDto?> GetByIdAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<OrderDetailsDto?>(null);
        public Task<OrderDetailsDto?> CreateAsync(CreateOrderDto order, CancellationToken cancellationToken) => Task.FromResult<OrderDetailsDto?>(null);
        public Task<IReadOnlyList<OrderItemDto>> GetItemsAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderItemDto>>([]);
        public Task<IReadOnlyList<OrderPieceDto>> GetPiecesAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderPieceDto>>([]);
        public Task<IReadOnlyList<OrderFabricDto>> GetFabricsAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderFabricDto>>([]);
        public Task<IReadOnlyList<OrderPaymentDto>> GetPaymentsAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderPaymentDto>>([]);
        public Task<IReadOnlyList<OrderTrackingDto>?> GetTrackingAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderTrackingDto>?>([]);
        public Task<DailyOrderWorkloadStatusDto> GetDailyWorkloadStatusAsync(CancellationToken cancellationToken) => Task.FromResult(OrderService.EvaluateDailyOrderWorkload(25));
        public Task<OrderDetailsDto?> CollectCustomerPaymentAsync(int orderId, decimal amount, string? paymentMethod, int? cashAccountId, string? referenceNumber, string? notes, CancellationToken cancellationToken) => Task.FromResult<OrderDetailsDto?>(null);
        public Task<OrderDetailsDto?> SettleCustomerBalanceAsync(int orderId, decimal amount, decimal discountAmount, string? paymentMethod, int? cashAccountId, string? referenceNumber, string? notes, CancellationToken cancellationToken) => Task.FromResult<OrderDetailsDto?>(null);
        public Task<OrderDeliveryDto?> GetDeliveryAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<OrderDeliveryDto?>(null);
        public Task<OrderDetailsDto?> DeliverAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<OrderDetailsDto?>(null);
        public Task<OrderDetailsDto?> WaiveRemainingBalanceAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<OrderDetailsDto?>(null);
        public Task<OrderDetailsDto?> RecognizeDeliveryRevenueAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<OrderDetailsDto?>(null);
        public Task<OrderDetailsDto?> CancelOrderAsync(int orderId, string? reason, string? cancelledBy, CancellationToken cancellationToken) => Task.FromResult<OrderDetailsDto?>(null);
        public Task<TailoringOrderEditSnapshot?> GetTailoringEditSnapshotAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<TailoringOrderEditSnapshot?>(null);
        public Task<TailoringOrderEditSnapshot> ApplyTailoringEditAsync(int orderId, TailoringOrderEditRequest request, string actor, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubTailoringOrderService(OrderDetailsDto? order = null) : IOrderService
    {
        public Task<IReadOnlyList<OrderListDto>> GetListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderListDto>>([]);
        public Task<OrderDetailsDto?> GetByIdAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<OrderDetailsDto?>(order);
        public Task<OrderDetailsDto?> CreateAsync(CreateOrderDto order, CancellationToken cancellationToken) => Task.FromResult<OrderDetailsDto?>(null);
        public Task<IReadOnlyList<OrderItemDto>> GetItemsAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderItemDto>>([]);
        public Task<IReadOnlyList<OrderPieceDto>> GetPiecesAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderPieceDto>>([]);
        public Task<IReadOnlyList<OrderFabricDto>> GetFabricsAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderFabricDto>>([]);
        public Task<IReadOnlyList<OrderPaymentDto>> GetPaymentsAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderPaymentDto>>([]);
        public Task<IReadOnlyList<OrderTrackingDto>?> GetTrackingAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderTrackingDto>?>([]);
        public Task<DailyOrderWorkloadStatusDto> GetDailyWorkloadStatusAsync(CancellationToken cancellationToken) => Task.FromResult(OrderService.EvaluateDailyOrderWorkload(25));
        public Task<OrderDetailsDto?> CollectCustomerPaymentAsync(int orderId, decimal amount, string? paymentMethod, int? cashAccountId, string? referenceNumber, string? notes, CancellationToken cancellationToken) => Task.FromResult<OrderDetailsDto?>(order);
        public Task<OrderDetailsDto?> SettleCustomerBalanceAsync(int orderId, decimal amount, decimal discountAmount, string? paymentMethod, int? cashAccountId, string? referenceNumber, string? notes, CancellationToken cancellationToken) => Task.FromResult<OrderDetailsDto?>(order);
        public Task<OrderDeliveryDto?> GetDeliveryAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<OrderDeliveryDto?>(null);
        public Task<OrderDetailsDto?> DeliverAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<OrderDetailsDto?>(order);
        public Task<OrderDetailsDto?> WaiveRemainingBalanceAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<OrderDetailsDto?>(order);
        public Task<OrderDetailsDto?> RecognizeDeliveryRevenueAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<OrderDetailsDto?>(order);
        public Task<OrderDetailsDto?> CancelOrderAsync(int orderId, string? reason, string? cancelledBy, CancellationToken cancellationToken) => Task.FromResult<OrderDetailsDto?>(order);
        public Task<TailoringOrderEditSnapshot?> GetTailoringEditSnapshotAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<TailoringOrderEditSnapshot?>(null);
        public Task<TailoringOrderEditSnapshot> ApplyTailoringEditAsync(int orderId, TailoringOrderEditRequest request, string actor, CancellationToken cancellationToken)
            => throw new TailoringOrderEditConflictException("تم استخدام معرف العملية نفسه مع طلب مختلف.");
    }

    private sealed class StubOrderRepository(OrderDetailsDto order) : IOrderRepository
    {
        public Task<IReadOnlyList<OrderListDto>> GetListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderListDto>>([]);
        public Task<OrderDetailsDto?> GetByIdAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<OrderDetailsDto?>(order);
        public Task<OrderDetailsDto?> CreateAsync(CreateOrderDto dto, CancellationToken cancellationToken) => Task.FromResult<OrderDetailsDto?>(order);
        public Task<IReadOnlyList<OrderItemDto>> GetItemsAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderItemDto>>([]);
        public Task<IReadOnlyList<OrderPieceDto>> GetPiecesAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderPieceDto>>([]);
        public Task<IReadOnlyList<OrderFabricDto>> GetFabricsAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderFabricDto>>([]);
        public Task<IReadOnlyList<OrderPaymentDto>> GetPaymentsAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderPaymentDto>>([]);
        public Task<IReadOnlyList<OrderTrackingDto>?> GetTrackingAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderTrackingDto>?>([]);
        public Task<int> CountCreatedOrdersForDayAsync(DateTime utcDayStart, DateTime utcDayEnd, CancellationToken cancellationToken) => Task.FromResult(0);
        public Task<OrderDetailsDto?> CollectCustomerPaymentAsync(int orderId, decimal amount, string? paymentMethod, int? cashAccountId, string? referenceNumber, string? notes, CancellationToken cancellationToken) => Task.FromResult<OrderDetailsDto?>(order);
        public Task<OrderDetailsDto?> SettleCustomerBalanceAsync(int orderId, decimal amount, decimal discountAmount, string? paymentMethod, int? cashAccountId, string? referenceNumber, string? notes, CancellationToken cancellationToken) => Task.FromResult<OrderDetailsDto?>(order);
        public Task<OrderDeliveryDto?> GetDeliveryAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<OrderDeliveryDto?>(null);
        public Task<OrderDetailsDto?> DeliverAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<OrderDetailsDto?>(order);
        public Task<OrderDetailsDto?> WaiveRemainingBalanceAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<OrderDetailsDto?>(order);
        public Task<OrderDetailsDto?> RecognizeDeliveryRevenueAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<OrderDetailsDto?>(order);
        public Task<OrderDetailsDto?> CancelOrderAsync(int orderId, string? reason, string? cancelledBy, CancellationToken cancellationToken) => Task.FromResult<OrderDetailsDto?>(order);
        public Task<TailoringOrderEditSnapshot> ApplyEditAsync(int orderId, TailoringOrderEditRequest request, string actor, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeUserContext(CurrentUserDto? user) : IAuthenticatedUserContext
    {
        public Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default) => Task.FromResult(user);
    }
}
