using System.ComponentModel.DataAnnotations;

namespace LUMAR_ERP_API_V2.DTOs.Orders;

public sealed record OrderListDto(int OrderId, string OrderNumber, int CustomerId, string? CustomerCode, string? CustomerName, string? PhoneNumber, DateTime OrderDate, DateTime? DeliveryDate, decimal TotalAmount, decimal PaidAmount, decimal RemainingAmount, string UrgencyStatus, string OrderStatus, string SaleCategory);
public sealed record OrderDetailsDto(int OrderId, string OrderNumber, int CustomerId, string? CustomerName, string? PhoneNumber, DateTime OrderDate, DateTime? DeliveryDate, decimal TotalAmount, decimal DiscountAmount, decimal PaidAmount, decimal RemainingAmount, string UrgencyStatus, string OrderStatus, string? Notes, DateTime CreatedDate, DateTime? UpdatedDate, string? CancellationReason, DateTime? CancelledAt, string? CancelledBy, string SaleCategory, bool RevenueRecognized, DateTime? RevenueRecognizedAt, bool RevenueReversalCreated, DateTime? RevenueReversalCreatedAt);

public class CreateOrderDto
{
    [Range(1, int.MaxValue)] public int CustomerId { get; init; }
    public DateTime? OrderDate { get; init; }
    public DateTime? DeliveryDate { get; init; }
    [Range(typeof(decimal), "0", "79228162514264337593543950335")] public decimal TotalAmount { get; init; }
    [Range(typeof(decimal), "0", "79228162514264337593543950335")] public decimal DiscountAmount { get; init; }
    [Range(typeof(decimal), "0", "79228162514264337593543950335")] public decimal AdvancePayment { get; init; }
    [Required, StringLength(30)] public string UrgencyStatus { get; init; } = "Normal";
    public string? Notes { get; init; }
    [Required, StringLength(50)] public string SaleCategory { get; init; } = "TailoringOrder";
    [StringLength(50)] public string? PaymentMethod { get; init; }
    [Range(1, int.MaxValue)] public int? CashAccountId { get; init; }
    [StringLength(100)] public string? RequestReference { get; init; }
    [Required, MinLength(1)] public IReadOnlyList<CreateOrderItemDto> Items { get; init; } = [];
}

public sealed class CreateOrderItemDto
{
    [Required, StringLength(50)] public string? PieceType { get; init; }
    [Range(1, 1000)] public int Quantity { get; init; } = 1;
    [StringLength(50)] public string? FabricCode { get; init; }
    [StringLength(100)] public string? FabricType { get; init; }
    [StringLength(100)] public string? FabricColor { get; init; }
    [StringLength(100)] public string? CatalogNumber { get; init; }
    [Range(typeof(decimal), "0", "79228162514264337593543950335")] public decimal? Consumption { get; init; }
    [StringLength(20)] public string? ConsumptionUnit { get; init; }
    [StringLength(500)] public string? Request1 { get; init; }
    [StringLength(500)] public string? Request2 { get; init; }
    [StringLength(500)] public string? SpecialRequest { get; init; }
    [StringLength(500)] public string? Notes1 { get; init; }
    [StringLength(500)] public string? Notes2 { get; init; }
    public string? MeasurementSnapshot { get; init; }
    public CreateOrderFabricDto? Fabric { get; init; }
    public int ProductTypeId { get; init; }
    public int? ImportedReadyMadeProductId { get; init; }
}

public sealed class CreateOrderFabricDto
{
    public int? InventoryItemId { get; init; }
    [StringLength(50)] public string? FabricCode { get; init; }
    [StringLength(100)] public string? FabricType { get; init; }
    [StringLength(100)] public string? FabricColor { get; init; }
    [Range(typeof(decimal), "0", "79228162514264337593543950335")] public decimal Quantity { get; init; }
    [Required, StringLength(20)] public string Unit { get; init; } = "Meter";
    [Range(typeof(decimal), "0", "79228162514264337593543950335")] public decimal UnitCost { get; init; }
}

public sealed class UpdateOrderDto : CreateOrderDto;

public sealed record OrderItemDto(int OrderItemId, int OrderId, string PieceType, int Quantity, string? FabricCode, string? FabricType, string? FabricColor, string? Request1, string? Request2, string? SpecialRequest, string? Notes1, string? Notes2, string? MeasurementSnapshot, string? TrackingCode, string? PieceStatus, DateTime CreatedDate, int? ProductTypeId = null, int? ImportedReadyMadeProductId = null);
public sealed record OrderFabricDto(int OrderItemFabricId, int OrderItemId, int? InventoryItemId, string? FabricCode, string? FabricType, string? FabricColor, decimal Quantity, string Unit, decimal UnitCost, decimal TotalCost, decimal ConsumedQuantity, decimal? YardPrice, DateTime CreatedDate);
public sealed record OrderPaymentDto(int PaymentId, int OrderId, int? InvoiceId, DateTime PaymentDate, decimal Amount, string? PaymentMethod, string? ReferenceNumber, string? Notes, DateTime CreatedDate, string PaymentKind);
public sealed record OrderTrackingDto(int TrackingCode, int? InvoiceId, int? CustomerId, string? ItemType, string? Status, string? CuttingEmployee, string? SewingEmployee, string? IroningEmployee, DateTime? CreatedDate, DateTime? CuttingDate, DateTime? SewingDate, DateTime? IroningDate, bool? IsCompleted, bool? IsDelivered, DateTime? DeliveryDate, bool? IsOnHold, string? HoldReason);
public sealed record OrderPieceDto(int PieceId, int OrderItemId, string TrackingCode, string PieceStatus, int PieceNumber, DateTime CreatedDate);
public sealed record OrderDeliveryDto(int OrderId, string OrderStatus, DateTime? DeliveryDate);
public sealed class CustomerPaymentRequestDto
{
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal Amount { get; init; }

    [StringLength(50)]
    public string? PaymentMethod { get; init; } = "Cash";

    [Range(1, int.MaxValue)]
    public int? CashAccountId { get; init; }

    [StringLength(100)]
    public string? ReferenceNumber { get; init; }

    [StringLength(500)]
    public string? Notes { get; init; }
}

public sealed class OrderSettlementRequestDto
{
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal Amount { get; init; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal DiscountAmount { get; init; }

    [StringLength(50)]
    public string? PaymentMethod { get; init; } = "Cash";

    [Range(1, int.MaxValue)]
    public int? CashAccountId { get; init; }

    [StringLength(100)]
    public string? ReferenceNumber { get; init; }

    [StringLength(500)]
    public string? Notes { get; init; }
}

public sealed class CancelOrderRequestDto
{
    public string? Reason { get; init; }
    public string? CancelledBy { get; init; }
}