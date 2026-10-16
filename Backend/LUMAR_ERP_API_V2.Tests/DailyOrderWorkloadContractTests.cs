using LUMAR_ERP_API_V2.Authorization;
using LUMAR_ERP_API_V2.Controllers;
using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.DTOs.Orders;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Services;
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
    }

    private sealed class FakeUserContext(CurrentUserDto? user) : IAuthenticatedUserContext
    {
        public Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default) => Task.FromResult(user);
    }
}
