using System.Collections.Generic;
using LUMAR_ERP_API_V2.DTOs.Loyalty;
using LUMAR_ERP_API_V2.DTOs.Messages;
using LUMAR_ERP_API_V2.DTOs.Orders;
using LUMAR_ERP_API_V2.DTOs.Production;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Services;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class MessageWriteServiceTests
{
    [Fact]
    public void MessageTemplateRenderer_RendersKnownTokensCaseInsensitive()
    {
        var renderer = new MessageTemplateRenderer();
        var body = "مرحباً {{CustomerName}}، تم إنشاء الطلب رقم {{OrderId}} بقيمة {{Amount}} ريال.";

        var rendered = renderer.Render(body, new Dictionary<string, object?>
        {
            ["CustomerName"] = "سالم",
            ["OrderId"] = 42,
            ["Amount"] = 149.99m,
        });

        Assert.Equal("مرحباً سالم، تم إنشاء الطلب رقم 42 بقيمة 149.99 ريال.", rendered);
    }

    [Fact]
    public async Task TryCreateOrderCreatedMessageAsync_IsIdempotent()
    {
        var repo = new FakeMessageRepository();
        var service = new MessageWriteService(repo, new MessageTemplateRenderer());

        var first = await service.TryCreateAsync(new MessageWriteRequest(
            CustomerId: 17,
            OrderId: 42,
            EventType: "OrderCreated",
            TemplateKey: "OrderCreated",
            PhoneNumber: "0500000000",
            ReferenceId: "ORD-42",
            ReferenceType: "Order",
            Context: new Dictionary<string, object?>
            {
                ["CustomerName"] = "سالم",
                ["OrderNumber"] = "ORD-42",
                ["Amount"] = 149.99m,
            }), CancellationToken.None);

        var second = await service.TryCreateAsync(new MessageWriteRequest(
            CustomerId: 17,
            OrderId: 42,
            EventType: "OrderCreated",
            TemplateKey: "OrderCreated",
            PhoneNumber: "0500000000",
            ReferenceId: "ORD-42",
            ReferenceType: "Order",
            Context: new Dictionary<string, object?>
            {
                ["CustomerName"] = "سالم",
                ["OrderNumber"] = "ORD-42",
                ["Amount"] = 149.99m,
            }), CancellationToken.None);

        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Single(repo.Messages);
        Assert.Single(repo.Notifications);
        Assert.Equal(repo.Messages[0].CustomerMessageId, repo.Notifications[0].CustomerMessageId);
    }

    [Fact]
    public async Task TryCreateAsync_UsesUniqueRewardGrantIdentity_ForReferralRewards()
    {
        var repo = new FakeMessageRepository();
        var service = new MessageWriteService(repo, new MessageTemplateRenderer());

        var rewardA = await service.TryCreateAsync(new MessageWriteRequest(
            CustomerId: 17,
            OrderId: 42,
            EventType: "ReferralPointsEarned",
            TemplateKey: "ReferralPointsEarned",
            PhoneNumber: "0500000000",
            ReferenceId: "RewardGranted:9001",
            ReferenceType: "Order",
            Context: new Dictionary<string, object?>
            {
                ["CustomerName"] = "مستفيد أ",
                ["OrderNumber"] = "ORD-42",
                ["OrderId"] = 42,
                ["PointsEarned"] = 10m,
            }), CancellationToken.None);

        var rewardB = await service.TryCreateAsync(new MessageWriteRequest(
            CustomerId: 17,
            OrderId: 42,
            EventType: "ReferralPointsEarned",
            TemplateKey: "ReferralPointsEarned",
            PhoneNumber: "0500000000",
            ReferenceId: "RewardGranted:9002",
            ReferenceType: "Order",
            Context: new Dictionary<string, object?>
            {
                ["CustomerName"] = "مستفيد أ",
                ["OrderNumber"] = "ORD-42",
                ["OrderId"] = 42,
                ["PointsEarned"] = 12m,
            }), CancellationToken.None);

        var repeatedA = await service.TryCreateAsync(new MessageWriteRequest(
            CustomerId: 17,
            OrderId: 42,
            EventType: "ReferralPointsEarned",
            TemplateKey: "ReferralPointsEarned",
            PhoneNumber: "0500000000",
            ReferenceId: "RewardGranted:9001",
            ReferenceType: "Order",
            Context: new Dictionary<string, object?>
            {
                ["CustomerName"] = "مستفيد أ",
                ["OrderNumber"] = "ORD-42",
                ["OrderId"] = 42,
                ["PointsEarned"] = 10m,
            }), CancellationToken.None);

        Assert.True(rewardA.Created);
        Assert.True(rewardB.Created);
        Assert.False(repeatedA.Created);
        Assert.Equal(2, repo.Messages.Count(x => x.EventType == "ReferralPointsEarned"));
        Assert.Equal(2, repo.Notifications.Count(x => x.EventType == "ReferralPointsEarned"));
    }

    [Fact]
    public async Task TryCreateAsync_WhenTemplateIsMissing_ReturnsFalse()
    {
        var repo = new FakeMessageRepository { TemplateExists = false };
        var service = new MessageWriteService(repo, new MessageTemplateRenderer());

        var result = await service.TryCreateAsync(new MessageWriteRequest(
            CustomerId: 17,
            OrderId: 42,
            EventType: "OrderCreated",
            TemplateKey: "MissingTemplate",
            PhoneNumber: "0500000000",
            ReferenceId: "ORD-42",
            ReferenceType: "Order",
            Context: new Dictionary<string, object?>
            {
                ["CustomerName"] = "سالم",
                ["OrderNumber"] = "ORD-42",
                ["Amount"] = 149.99m,
            }), CancellationToken.None);

        Assert.False(result.Created);
        Assert.Empty(repo.Messages);
        Assert.Empty(repo.Notifications);
    }

    [Fact]
    public async Task TryCreateAsync_WhenNotificationCreationFails_DoesNotLeavePartialState()
    {
        var repo = new FakeMessageRepository { FailNotificationCreation = true };
        var service = new MessageWriteService(repo, new MessageTemplateRenderer());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.TryCreateAsync(new MessageWriteRequest(
            CustomerId: 17,
            OrderId: 42,
            EventType: "OrderCreated",
            TemplateKey: "OrderCreated",
            PhoneNumber: "0500000000",
            ReferenceId: "ORD-42",
            ReferenceType: "Order",
            Context: new Dictionary<string, object?>
            {
                ["CustomerName"] = "سالم",
                ["OrderNumber"] = "ORD-42",
                ["Amount"] = 149.99m,
            }), CancellationToken.None));

        Assert.Empty(repo.Messages);
        Assert.Empty(repo.Notifications);
    }

    [Fact]
    public async Task ProductionService_AdvancePieceStageAsync_WhenOrderIsNotOfficiallyReady_DoesNotSendOrderReadyMessage()
    {
        var messageService = new RecordingMessageWriteService();
        var order = new OrderDetailsDto(
            99,
            "ORD-99",
            7,
            "سالم",
            "0500000099",
            DateTime.UtcNow,
            null,
            150m,
            0m,
            0m,
            150m,
            "Normal",
            "Confirmed",
            "Pending",
            DateTime.UtcNow,
            null,
            null,
            null,
            null,
            "TailoringOrder",
            false,
            null,
            false,
            null);

        var productionRepository = new FakeProductionRepository(
            new PieceDto(12, 1, "TRK-99", "Cutting", 1, DateTime.UtcNow, "YALAQ", 99, "ORD-99", 9),
            new ProductionTrackingAdvanceResultDto(12, "TRK-99", "Cutting", "Ironing", "Ironing", "تم التقدم إلى Ironing", true));
        var orderRepository = new FakeOrderRepository(order);
        var service = new ProductionService(productionRepository, new FakeOrderLoyaltyIntegrationService(), orderRepository, messageService);

        await service.AdvancePieceStageAsync(new ProductionTrackingAdvanceRequestDto(12, "TRK-99", "YALAQ", "Ironing", "SCN-1", "EMP-1", "REF-1", 9), CancellationToken.None);

        Assert.Empty(messageService.Requests);
    }

    [Fact]
    public async Task ProductionService_AdvancePieceStageAsync_WhenOrderBecomesReadyForDelivery_SendsOrderReadyExactlyOnce()
    {
        var messageService = new RecordingMessageWriteService();
        var order = new OrderDetailsDto(
            100,
            "ORD-100",
            7,
            "سالم",
            "0500000100",
            DateTime.UtcNow,
            null,
            150m,
            0m,
            0m,
            150m,
            "Normal",
            "ReadyForDelivery",
            "Ready",
            DateTime.UtcNow,
            null,
            null,
            null,
            null,
            "TailoringOrder",
            false,
            null,
            false,
            null);

        var productionRepository = new FakeProductionRepository(
            new PieceDto(13, 1, "TRK-100", "Ready", 1, DateTime.UtcNow, "YALAQ", 100, "ORD-100", 9),
            new ProductionTrackingAdvanceResultDto(13, "TRK-100", "Ready", "ReadyForDelivery", null, "Order is ready for delivery and can be packed.", true));
        var orderRepository = new FakeOrderRepository(order);
        var service = new ProductionService(productionRepository, new FakeOrderLoyaltyIntegrationService(), orderRepository, messageService);

        await service.AdvancePieceStageAsync(new ProductionTrackingAdvanceRequestDto(13, "TRK-100", "YALAQ", "ReadyForDelivery", "SCN-2", "EMP-2", "REF-2", 9), CancellationToken.None);

        Assert.Single(messageService.Requests);
        Assert.Equal("OrderReady", messageService.Requests[0].EventType);
        Assert.Equal("ORD-100", messageService.Requests[0].ReferenceId);
        Assert.Equal(7, messageService.Requests[0].CustomerId);
    }

    [Fact]
    public async Task OrderService_DeliverAsync_WhenOrderDeliveryIsNotOfficiallyCompleted_DoesNotSendOrderDeliveredMessage()
    {
        var messageService = new RecordingMessageWriteService();
        var service = new OrderService(
            new FakeOrderRepository(new OrderDetailsDto(
                200,
                "ORD-200",
                8,
                "أحمد",
                "0500000200",
                DateTime.UtcNow,
                null,
                200m,
                0m,
                0m,
                200m,
                "Normal",
                "Confirmed",
                null,
                DateTime.UtcNow,
                null,
                null,
                null,
                null,
                "TailoringOrder",
                false,
                null,
                false,
                null), deliverResult: null),
            new FakeVipLevelEvaluationService(),
            new FakeOrderLoyaltyIntegrationService(),
            messageService);

        var result = await service.DeliverAsync(200, CancellationToken.None);

        Assert.Null(result);
        Assert.Empty(messageService.Requests);
    }

    [Fact]
    public async Task OrderService_DeliverAsync_WhenOrderIsOfficiallyDelivered_SendsOrderDeliveredOnce()
    {
        var messageService = new RecordingMessageWriteService();
        var order = new OrderDetailsDto(
            201,
            "ORD-201",
            8,
            "أحمد",
            "0500000201",
            DateTime.UtcNow,
            null,
            200m,
            0m,
            200m,
            0m,
            "Normal",
            "ReadyForDelivery",
            null,
            DateTime.UtcNow,
            null,
            null,
            null,
            null,
            "TailoringOrder",
            false,
            null,
            false,
            null);

        var service = new OrderService(
            new FakeOrderRepository(order, order),
            new FakeVipLevelEvaluationService(),
            new FakeOrderLoyaltyIntegrationService(),
            messageService);

        var result = await service.DeliverAsync(201, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(messageService.Requests);
        Assert.Equal("OrderDelivered", messageService.Requests[0].EventType);
        Assert.Equal("ORD-201", messageService.Requests[0].ReferenceId);
    }

    [Fact]
    public async Task TryCreateAsync_UsesIdempotencyForReadyAndDeliveredBusinessEvents()
    {
        var repo = new FakeMessageRepository();
        var service = new MessageWriteService(repo, new MessageTemplateRenderer());

        var readyOne = await service.TryCreateAsync(new MessageWriteRequest(
            7,
            42,
            "OrderReady",
            "OrderReady",
            "0500000042",
            "ORD-42",
            "Order",
            new Dictionary<string, object?>
            {
                ["CustomerName"] = "سالم",
                ["OrderNumber"] = "ORD-42",
                ["OrderId"] = 42,
            }), CancellationToken.None);

        var readyTwo = await service.TryCreateAsync(new MessageWriteRequest(
            7,
            42,
            "OrderReady",
            "OrderReady",
            "0500000042",
            "ORD-42",
            "Order",
            new Dictionary<string, object?>
            {
                ["CustomerName"] = "سالم",
                ["OrderNumber"] = "ORD-42",
                ["OrderId"] = 42,
            }), CancellationToken.None);

        var deliveredOne = await service.TryCreateAsync(new MessageWriteRequest(
            7,
            42,
            "OrderDelivered",
            "OrderDelivered",
            "0500000042",
            "ORD-42",
            "Order",
            new Dictionary<string, object?>
            {
                ["CustomerName"] = "سالم",
                ["OrderNumber"] = "ORD-42",
                ["OrderId"] = 42,
            }), CancellationToken.None);

        var deliveredTwo = await service.TryCreateAsync(new MessageWriteRequest(
            7,
            42,
            "OrderDelivered",
            "OrderDelivered",
            "0500000042",
            "ORD-42",
            "Order",
            new Dictionary<string, object?>
            {
                ["CustomerName"] = "سالم",
                ["OrderNumber"] = "ORD-42",
                ["OrderId"] = 42,
            }), CancellationToken.None);

        Assert.True(readyOne.Created);
        Assert.False(readyTwo.Created);
        Assert.True(deliveredOne.Created);
        Assert.False(deliveredTwo.Created);
        Assert.Equal(2, repo.Messages.Count);
        Assert.Equal(2, repo.Notifications.Count);
    }

    [Fact]
    public async Task ReadyMadeSalesService_SendsReadyMadeSaleCompletedMessage()
    {
        var messageService = new RecordingMessageWriteService();
        var readyMadeSalesService = new ReadyMadeSalesService(
            new FakeReadyMadeSalesRepository(),
            new FakeOrderLoyaltyIntegrationService(),
            messageService);

        var result = await readyMadeSalesService.CreateAsync(new CreateReadyMadeSaleDto
        {
            CustomerId = 7,
            PaymentType = "Cash",
            PaidAmount = 100m,
            CashAccountId = 1,
            Items =
            [
                new CreateReadyMadeSaleItemDto
                {
                    ReadyMadeInventoryProductId = 88,
                    ProductTypeId = 9,
                    Quantity = 1,
                    UnitPrice = 100m,
                }
            ]
        }, CancellationToken.None);

        Assert.Equal(101, result.OrderId);
        Assert.Single(messageService.Requests);
        Assert.Equal("ReadyMadeSaleCompleted", messageService.Requests[0].EventType);
        Assert.Equal(7, messageService.Requests[0].CustomerId);
    }

    private sealed class RecordingMessageWriteService : IMessageWriteService
    {
        public List<MessageWriteRequest> Requests { get; } = new();

        public Task<MessageWriteResult> TryCreateAsync(MessageWriteRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new MessageWriteResult(true, 1, 1, request.TemplateKey, "x", "y"));
        }
    }

    private sealed class FakeOrderRepository(OrderDetailsDto? currentOrder = null, OrderDetailsDto? deliveredOrder = null, bool? deliverResult = null) : IOrderRepository
    {
        public Task<IReadOnlyList<OrderListDto>> GetListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderListDto>>(Array.Empty<OrderListDto>());
        public Task<OrderDetailsDto?> GetByIdAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult(currentOrder);
        public Task<OrderDetailsDto?> CreateAsync(CreateOrderDto order, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<OrderItemDto>> GetItemsAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderItemDto>>(Array.Empty<OrderItemDto>());
        public Task<IReadOnlyList<OrderPieceDto>> GetPiecesAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderPieceDto>>(Array.Empty<OrderPieceDto>());
        public Task<IReadOnlyList<OrderFabricDto>> GetFabricsAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderFabricDto>>(Array.Empty<OrderFabricDto>());
        public Task<IReadOnlyList<OrderPaymentDto>> GetPaymentsAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderPaymentDto>>(Array.Empty<OrderPaymentDto>());
        public Task<IReadOnlyList<OrderTrackingDto>?> GetTrackingAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderTrackingDto>?>(Array.Empty<OrderTrackingDto>());
        public Task<OrderDetailsDto?> CollectCustomerPaymentAsync(int orderId, decimal amount, string? paymentMethod, int? cashAccountId, string? referenceNumber, string? notes, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<OrderDetailsDto?> SettleCustomerBalanceAsync(int orderId, decimal amount, decimal discountAmount, string? paymentMethod, int? cashAccountId, string? referenceNumber, string? notes, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<OrderDeliveryDto?> GetDeliveryAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<OrderDeliveryDto?>(null);
        public Task<OrderDetailsDto?> DeliverAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult(deliverResult is null ? deliveredOrder : deliverResult.Value ? deliveredOrder : null);
        public Task<OrderDetailsDto?> WaiveRemainingBalanceAsync(int orderId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<OrderDetailsDto?> RecognizeDeliveryRevenueAsync(int orderId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<OrderDetailsDto?> CancelOrderAsync(int orderId, string? reason, string? cancelledBy, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeProductionRepository(PieceDto piece, ProductionTrackingAdvanceResultDto result) : IProductionRepository
    {
        public Task<IReadOnlyList<PieceDto>> GetPiecesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PieceDto>>(Array.Empty<PieceDto>());
        public Task<IReadOnlyList<PieceDto>> GetReadyMadePiecesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PieceDto>>(Array.Empty<PieceDto>());
        public Task<PieceDto?> GetPieceByIdAsync(int pieceId, CancellationToken cancellationToken) => Task.FromResult<PieceDto?>(piece);
        public Task<PieceDto?> GetReadyMadePieceByIdAsync(int pieceId, CancellationToken cancellationToken) => Task.FromResult<PieceDto?>(null);
        public Task<WorkCardDto?> GetWorkCardAsync(int pieceId, CancellationToken cancellationToken) => Task.FromResult<WorkCardDto?>(null);
        public Task<WorkCardDto?> GetReadyMadeWorkCardAsync(int pieceId, CancellationToken cancellationToken) => Task.FromResult<WorkCardDto?>(null);
        public Task<IReadOnlyList<TrackingEventDto>> GetPieceTrackingAsync(int pieceId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TrackingEventDto>>(Array.Empty<TrackingEventDto>());
        public Task<IReadOnlyList<ProductionStageDto>> GetStagesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProductionStageDto>>(Array.Empty<ProductionStageDto>());
        public Task<ProductionDashboardDto> GetDashboardAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<FactoryMonitoringDashboardDto> GetFactoryMonitoringAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ReadyMadeProductionOrderDto>> GetReadyMadeOrdersAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ReadyMadeProductionOrderDto>>(Array.Empty<ReadyMadeProductionOrderDto>());
        public Task<ReadyMadeProductionOrderDto?> GetReadyMadeOrderByIdAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<ReadyMadeProductionOrderDto?>(null);
        public Task<IReadOnlyList<ReadyMadeProductionOrderItemDto>> GetReadyMadeOrderItemsAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ReadyMadeProductionOrderItemDto>>(Array.Empty<ReadyMadeProductionOrderItemDto>());
        public Task<IReadOnlyList<ReadyMadeProductionPieceDto>> GetReadyMadeItemPiecesAsync(int itemId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ReadyMadeProductionPieceDto>>(Array.Empty<ReadyMadeProductionPieceDto>());
        public Task<IReadOnlyList<TrackingEventDto>> GetReadyMadePieceTrackingAsync(int pieceInstanceId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TrackingEventDto>>(Array.Empty<TrackingEventDto>());
        public Task<ReadyMadeProductionOrderCreateResultDto> CreateReadyMadeOrderAsync(ReadyMadeProductionOrderCreateDto order, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProductionDeliveryDto>> GetDeliveriesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProductionDeliveryDto>>(Array.Empty<ProductionDeliveryDto>());
        public Task<ProductionTrackingRouteDto?> GetPieceRouteAsync(int pieceId, CancellationToken cancellationToken) => Task.FromResult<ProductionTrackingRouteDto?>(null);
        public Task<ProductionTrackingRouteDto?> GetPieceRouteByTrackingCodeAsync(string trackingCode, CancellationToken cancellationToken) => Task.FromResult<ProductionTrackingRouteDto?>(null);
        public Task<ProductionTrackingRouteDto?> GetReadyMadePieceRouteAsync(int pieceId, CancellationToken cancellationToken) => Task.FromResult<ProductionTrackingRouteDto?>(null);
        public Task<ProductionTrackingRouteDto?> GetReadyMadePieceRouteByTrackingCodeAsync(string trackingCode, CancellationToken cancellationToken) => Task.FromResult<ProductionTrackingRouteDto?>(null);
        public Task<ProductionTrackingAdvanceResultDto?> AdvancePieceStageAsync(ProductionTrackingAdvanceRequestDto request, CancellationToken cancellationToken) => Task.FromResult<ProductionTrackingAdvanceResultDto?>(result);
    }

    private sealed class FakeReadyMadeSalesRepository : IReadyMadeSalesRepository
    {
        public Task<ReadyMadeSaleResultDto> CreateAsync(CreateReadyMadeSaleDto sale, CancellationToken cancellationToken)
            => Task.FromResult(new ReadyMadeSaleResultDto(
                101,
                "RMS-101",
                1,
                "INV-1",
                sale.CustomerId,
                "Test Customer",
                "REF-1",
                sale.PaymentType,
                100m,
                0m,
                100m,
                100m,
                0m,
                [new ReadyMadeSaleLineDto(88, null, 9, "Ready item", 1, 100m, 100m, 80m)]));
    }

    private sealed class FakeOrderLoyaltyIntegrationService : IOrderLoyaltyIntegrationService
    {
        public Task<OrderLoyaltyProcessingResult?> ProcessIfEligibleAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<OrderLoyaltyProcessingResult?>(null);
        public Task<OrderLoyaltyProcessingResult> ProcessOrderAsync(int orderId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeVipLevelEvaluationService : IVipLevelEvaluationService
    {
        public Task<IReadOnlyList<VipLevelEvaluationCriteriaDto>> GetCriteriaAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<VipLevelEvaluationCriteriaDto>>(Array.Empty<VipLevelEvaluationCriteriaDto>());

        public Task<IReadOnlyList<VipCustomerListItemDto>> GetClassifiedCustomersAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<VipCustomerListItemDto>>(Array.Empty<VipCustomerListItemDto>());

        public Task<VipLevelEvaluationDto?> GetCustomerEvaluationAsync(int customerId, CancellationToken cancellationToken)
            => Task.FromResult<VipLevelEvaluationDto?>(null);

        public Task<VipLevelEvaluationDto?> EvaluateCustomerAsync(int customerId, CancellationToken cancellationToken)
            => Task.FromResult<VipLevelEvaluationDto?>(null);

        public Task<IReadOnlyList<VipLevelEvaluationDto>> EvaluateAllAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<VipLevelEvaluationDto>>(Array.Empty<VipLevelEvaluationDto>());

        public Task<IReadOnlyList<VipLevelEvaluationDto>> EvaluateNetworkAsync(int customerId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<VipLevelEvaluationDto>>(Array.Empty<VipLevelEvaluationDto>());
    }

    private sealed class FakeMessageRepository : IMessageRepository
    {
        public List<CustomerMessageDto> Messages { get; } = new();
        public List<CustomerNotificationDto> Notifications { get; } = new();
        public HashSet<string> ProcessedKeys { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool TemplateExists { get; set; } = true;
        public bool FailNotificationCreation { get; set; }

        public Task<IReadOnlyList<CustomerMessageDto>> GetCustomerMessagesAsync(CancellationToken c) => Task.FromResult<IReadOnlyList<CustomerMessageDto>>(Messages);
        public Task<CustomerMessageDto?> GetCustomerMessageAsync(int id, CancellationToken c) => Task.FromResult<CustomerMessageDto?>(Messages.FirstOrDefault(x => x.CustomerMessageId == id));
        public Task<IReadOnlyList<CustomerMessageDto>> GetCustomerMessagesAsync(int customerId, CancellationToken c) => Task.FromResult<IReadOnlyList<CustomerMessageDto>>(Messages.Where(x => x.CustomerId == customerId).ToList());
        public Task<IReadOnlyList<CustomerNotificationDto>> GetNotificationsAsync(CancellationToken c) => Task.FromResult<IReadOnlyList<CustomerNotificationDto>>(Notifications);
        public Task<IReadOnlyList<CustomerNotificationDto>> GetNotificationsAsync(int customerId, CancellationToken c) => Task.FromResult<IReadOnlyList<CustomerNotificationDto>>(Notifications.Where(x => x.CustomerId == customerId).ToList());
        public Task<IReadOnlyList<MessageTemplateDto>> GetTemplatesAsync(CancellationToken c) => Task.FromResult<IReadOnlyList<MessageTemplateDto>>(new[]
        {
            new MessageTemplateDto(1, "dbo.Orders", "OrderCreated", "إنشاء الطلب", "تم إنشاء الطلب", "sms", "مرحباً {CustomerName}، تم إنشاء الطلب رقم {OrderNumber} بقيمة {Amount} ريال.", true, DateTime.UtcNow, DateTime.UtcNow)
        });
        public Task<IReadOnlyList<MessageTemplateDto>> GetTemplatesByIdAsync(int id, CancellationToken c) => Task.FromResult<IReadOnlyList<MessageTemplateDto>>(new[]
        {
            new MessageTemplateDto(1, "dbo.Orders", "OrderCreated", "إنشاء الطلب", "تم إنشاء الطلب", "sms", "مرحباً {CustomerName}، تم إنشاء الطلب رقم {OrderNumber} بقيمة {Amount} ريال.", true, DateTime.UtcNow, DateTime.UtcNow)
        });
        public Task<IReadOnlyList<SentMessageDto>> GetSentAsync(CancellationToken c) => Task.FromResult<IReadOnlyList<SentMessageDto>>(Array.Empty<SentMessageDto>());
        public Task<IReadOnlyList<SentLogDto>> GetSentLogAsync(CancellationToken c) => Task.FromResult<IReadOnlyList<SentLogDto>>(Array.Empty<SentLogDto>());
        public Task<MessageTemplateRecord?> GetActiveTemplateAsync(string templateKey, CancellationToken c)
            => TemplateExists ? Task.FromResult<MessageTemplateRecord?>(new MessageTemplateRecord(1, templateKey, "إنشاء الطلب", "sms", "مرحباً {{CustomerName}}، تم إنشاء الطلب رقم {{OrderNumber}} بقيمة {{Amount}} ريال.")) : Task.FromResult<MessageTemplateRecord?>(null);

        public Task<bool> HasProcessedAsync(string idempotencyKey, CancellationToken c) => Task.FromResult(ProcessedKeys.Contains(idempotencyKey));

        public Task<(int? MessageId, int? NotificationId)> CreateCustomerMessageAndNotificationAsync(int customerId, int? orderId, string eventType, string templateKey, string channel, string title, string body, string phoneNumber, string referenceId, string referenceType, string idempotencyKey, CancellationToken c)
        {
            if (FailNotificationCreation)
            {
                throw new InvalidOperationException("Notification write failed.");
            }

            var messageId = Messages.Count + 1;
            var notificationId = Notifications.Count + 1;
            ProcessedKeys.Add(idempotencyKey);
            Messages.Add(new CustomerMessageDto(messageId, customerId, orderId, eventType, templateKey, channel, title, body, "Pending", DateTime.UtcNow, null, null, 0, null));
            Notifications.Add(new CustomerNotificationDto(notificationId, customerId, orderId, eventType, title, body, "Unread", false, DateTime.UtcNow, null, messageId));
            return Task.FromResult((MessageId: (int?)messageId, NotificationId: (int?)notificationId));
        }

        public Task<int?> CreateCustomerMessageAsync(int customerId, int? orderId, string eventType, string templateKey, string channel, string title, string body, string phoneNumber, string referenceId, string referenceType, string idempotencyKey, CancellationToken c)
        {
            var id = Messages.Count + 1;
            ProcessedKeys.Add(idempotencyKey);
            Messages.Add(new CustomerMessageDto(id, customerId, orderId, eventType, templateKey, channel, title, body, "Pending", DateTime.UtcNow, null, null, 0, null));
            return Task.FromResult<int?>(id);
        }
        public Task<int> CreateCustomerNotificationAsync(int customerId, int? orderId, string eventType, string title, string body, int customerMessageId, CancellationToken c)
        {
            var id = Notifications.Count + 1;
            Notifications.Add(new CustomerNotificationDto(id, customerId, orderId, eventType, title, body, "Unread", false, DateTime.UtcNow, null, customerMessageId));
            return Task.FromResult(id);
        }
    }
}
