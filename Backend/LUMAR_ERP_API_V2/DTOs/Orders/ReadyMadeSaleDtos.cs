using System.ComponentModel.DataAnnotations;

namespace LUMAR_ERP_API_V2.DTOs.Orders;

public sealed class CreateReadyMadeSaleDto
{
    [Range(1, int.MaxValue)]
    public int CustomerId { get; init; }

    [StringLength(50)]
    public string? EmployeeCode { get; init; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal DiscountAmount { get; init; }

    [Required, StringLength(30)]
    public string PaymentType { get; init; } = "Cash";

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal PaidAmount { get; init; }

    [StringLength(100)]
    public string? SaleReference { get; init; }

    [StringLength(500)]
    public string? Notes { get; init; }

    [Required, MinLength(1)]
    public IReadOnlyList<CreateReadyMadeSaleItemDto> Items { get; init; } = [];
}

public sealed class CreateReadyMadeSaleItemDto
{
    public int? ReadyMadeInventoryProductId { get; init; }
    public int? ImportedReadyMadeProductId { get; init; }
    public int? ProductTypeId { get; init; }

    [Range(1, 1000)]
    public int Quantity { get; init; } = 1;

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal UnitPrice { get; init; }
}

public sealed record ReadyMadeSaleResultDto(
    int OrderId,
    string OrderNumber,
    int InvoiceId,
    string InvoiceNumber,
    int CustomerId,
    string? CustomerName,
    string SaleReference,
    string PaymentType,
    decimal TotalAmount,
    decimal DiscountAmount,
    decimal NetAmount,
    decimal PaidAmount,
    decimal RemainingAmount,
    IReadOnlyList<ReadyMadeSaleLineDto> Items);

public sealed record ReadyMadeSaleLineDto(
    int? ReadyMadeInventoryProductId,
    int? ImportedReadyMadeProductId,
    int? ProductTypeId,
    string ItemName,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice,
    decimal UnitCost);