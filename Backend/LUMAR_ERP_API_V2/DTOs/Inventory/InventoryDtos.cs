using System.ComponentModel.DataAnnotations;

namespace LUMAR_ERP_API_V2.DTOs.Inventory;

public sealed record InventoryItemDto(int InventoryItemId, string ItemCode, string ItemName, string Category, string Unit, decimal CurrentQuantity, decimal AvailableQuantity, decimal ReservedQuantity, bool IsActive, DateTime CreatedAt, DateTime? UpdatedAt, string? Barcode, string? FabricCategory, string? FabricColor, decimal? FabricWidth, string? FabricWidthUnit, decimal? InchPrice, decimal? YardPrice);
public sealed record InventoryTransactionDto(int TransactionId, int InventoryItemId, string TransactionType, decimal Quantity, string? ReferenceNumber, string? Notes, DateTime CreatedAt, decimal? TotalCostImpact, decimal? UnitCost);
public sealed record InventoryWarehouseSummaryDto(string WarehouseKey, decimal TotalInputValue, decimal CurrentInventoryValue);
public sealed record FabricDto(string SourceTable, int? FabricId, string? FabricCode, string? FabricName, decimal? FabricPrice, bool? IsActive, int? InventoryFabricCode, string? InventoryFabricName, string? Unit, string? Color, string? CatalogNumber, decimal? QuantityYard, decimal? QuantityInch, decimal? TotalRollCost, decimal? PricePerYard, decimal? PricePerInch, decimal? UsedQuantity, decimal? AvailableQuantity);
public sealed record ReadyMadeProductDto(int ReadyMadeInventoryProductId, int ReadyMadeProductionOrderId, int ReadyMadeProductionOrderItemId, int ReadyMadeProductionOrderPieceInstanceId, int? ProductTypeId, string ProductionOrderNumber, string ProductionName, string PieceType, int PieceNumber, string TrackingCode, string? FabricCode, string? FabricType, string? FabricColor, string? CatalogNumber, string? FabricUnit, string? FabricWidth, string? FabricWidthUnit, decimal? ActualCost, decimal? SuggestedSellingPrice, string? MeasurementSnapshot, DateTime ReadyForSaleAt, string Status, string Source, string? Notes, bool IsActive, DateTime CreatedAt, string? ProductTypeName = null);
public sealed record ImportedReadyMadeProductDto(int ImportedReadyMadeProductId, string ProductName, string ProductType, string ProductCode, string Unit, decimal Quantity, decimal PurchasePrice, decimal SellingPrice, bool IsActive, decimal? AlertThreshold, string? Notes, string Category, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed class CreateImportedProductDto
{
    [Required, StringLength(200)]
    public string ProductName { get; init; } = string.Empty;

    [StringLength(150)]
    public string? ProductType { get; init; }

    [Required, StringLength(100)]
    public string ProductCode { get; init; } = string.Empty;

    [Required, StringLength(50)]
    public string Unit { get; init; } = string.Empty;

    [Range(typeof(decimal), "0.01", "1000000000")]
    public decimal Quantity { get; init; }

    [Range(typeof(decimal), "0.01", "1000000000")]
    public decimal PurchasePrice { get; init; }

    [Range(typeof(decimal), "0.01", "1000000000")]
    public decimal SellingPrice { get; init; }

    [Range(0, int.MaxValue)]
    public int? SupplierId { get; init; }

    [StringLength(1000)]
    public string? Notes { get; init; }

    [StringLength(100)]
    public string? Category { get; init; }

    public bool RenewExisting { get; init; }
}

public sealed class CreateToolItemDto
{
    [Required, StringLength(200)]
    public string ProductName { get; init; } = string.Empty;

    [StringLength(150)]
    public string? ProductType { get; init; }

    [StringLength(100)]
    public string? ProductCode { get; init; }

    [Required, StringLength(50)]
    public string Unit { get; init; } = string.Empty;

    [Range(typeof(decimal), "0.01", "1000000000")]
    public decimal Quantity { get; init; }

    [Range(typeof(decimal), "0.01", "1000000000")]
    public decimal UnitPrice { get; init; }

    [Range(0, int.MaxValue)]
    public int? SupplierId { get; init; }

    [StringLength(100)]
    public string? InvoiceNumber { get; init; }

    [StringLength(1000)]
    public string? Notes { get; init; }

    public bool RenewExisting { get; init; }
}

public sealed class CreateFabricBatchDto
{
    [Required, Range(1, int.MaxValue)]
    public int SupplierId { get; init; }

    [Required, StringLength(100)]
    public string InvoiceNumber { get; init; } = string.Empty;

    public DateTime? PurchaseDate { get; init; }

    [StringLength(1000)]
    public string? Notes { get; init; }

    [Required, MinLength(1)]
    public IReadOnlyList<CreateFabricRollDto> Rolls { get; init; } = [];
}

public sealed class CreateFabricRollDto
{
    [Required, StringLength(50)]
    public string FabricCode { get; init; } = string.Empty;

    [StringLength(100)]
    public string? CatalogNumber { get; init; }

    [Required, StringLength(100)]
    public string FabricType { get; init; } = string.Empty;

    [StringLength(100)]
    public string? FabricColor { get; init; }

    [Range(typeof(decimal), "0.01", "10000")]
    public decimal FabricWidth { get; init; }

    [Range(typeof(decimal), "0.01", "1000000")]
    public decimal QuantityYards { get; init; }

    [Range(typeof(decimal), "0.01", "10000000")]
    public decimal YardPrice { get; init; }
}

public sealed record FabricBatchResultDto(
    int TotalRolls,
    decimal TotalYards,
    decimal TotalCost,
    string ReferenceNumber,
    DateTime CreatedAt
);

public sealed class ReceiveFabricInventoryDto
{
    [Range(1, int.MaxValue)]
    public int GoodsReceiptItemId { get; init; }

    [Required, StringLength(100)]
    public string ItemCode { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string FabricTypeCode { get; init; } = string.Empty;

    [StringLength(100)]
    public string? RollCode { get; init; }

    [StringLength(100)]
    public string? ColorValue { get; init; }

    [Range(1, 2)]
    public short UnitId { get; init; } = 1;

    [Required, StringLength(100)]
    public string OpposingLedgerAccountCode { get; init; } = string.Empty;

    public Guid SourceOperationId { get; init; }
}

public sealed class ReceiveConsumableInventoryDto
{
    [Range(1, int.MaxValue)]
    public int GoodsReceiptItemId { get; init; }

    [Required, StringLength(100)]
    public string ItemCode { get; init; } = string.Empty;

    [Range(3, 3)]
    public short UnitId { get; init; } = 3;

    [Required, StringLength(100)]
    public string OpposingLedgerAccountCode { get; init; } = string.Empty;

    public Guid SourceOperationId { get; init; }
}

public sealed class ConsumeFabricInventoryDto
{
    [Range(1, long.MaxValue)]
    public long FabricRollId { get; init; }

    [Range(1, int.MaxValue)]
    public int OrderItemId { get; init; }

    [Range(1, int.MaxValue)]
    public int? PieceId { get; init; }

    [Range(typeof(decimal), "0.000001", "1000000000")]
    public decimal Quantity { get; init; }

    [Range(1, int.MaxValue)]
    public int? ConfirmedByUserId { get; init; }

    public Guid SourceOperationId { get; init; }
}

public sealed class ConsumeConsumableInventoryDto
{
    [Range(1, int.MaxValue)]
    public int ProductionOrderId { get; init; }

    [Range(1, int.MaxValue)]
    public int? ProductionBatchId { get; init; }

    [Range(1, int.MaxValue)]
    public int InventoryItemId { get; init; }

    [Range(1, int.MaxValue)]
    public int? ProductMaterialId { get; init; }

    [Range(typeof(decimal), "0.000001", "1000000000")]
    public decimal Quantity { get; init; }

    [Range(3, 3)]
    public short UnitId { get; init; } = 3;

    [Range(1, int.MaxValue)]
    public int? ConfirmedByUserId { get; init; }

    public Guid SourceOperationId { get; init; }
}

public sealed record InventoryFoundationPostingResultDto(
    long SourceRecordId,
    int InventoryItemId,
    string ItemCode,
    decimal Quantity,
    decimal OperationalAmount,
    decimal PostingAmount,
    long AccountingEventId,
    int InventoryTransactionId,
    bool IsExisting
);