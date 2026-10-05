using System.ComponentModel.DataAnnotations;

namespace LUMAR_ERP_API_V2.DTOs.Inventory;

public sealed record InventoryItemDto(int InventoryItemId, string ItemCode, string ItemName, string Category, string Unit, decimal CurrentQuantity, decimal AvailableQuantity, decimal ReservedQuantity, bool IsActive, DateTime CreatedAt, DateTime? UpdatedAt, string? Barcode, string? FabricCategory, string? FabricColor, decimal? FabricWidth, string? FabricWidthUnit, decimal? InchPrice, decimal? YardPrice);
public sealed record InventoryTransactionDto(int TransactionId, int InventoryItemId, string TransactionType, decimal Quantity, string? ReferenceNumber, string? Notes, DateTime CreatedAt, decimal? TotalCostImpact, decimal? UnitCost);
public sealed record InventoryWarehouseSummaryDto(string WarehouseKey, decimal TotalInputValue, decimal CurrentInventoryValue);
public sealed record InventoryWarehouseDto(int WarehouseId, string WarehouseCode, string WarehouseName, bool IsActive);
public sealed record GoodsReceiptDifferenceDto(long GoodsReceiptDifferenceId, int GoodsReceiptItemId, string DifferenceType, decimal? ExpectedQuantity, decimal? ActualQuantity, decimal? ExpectedUnitCost, decimal? ActualUnitCost);
public sealed record PendingGoodsReceiptStorageDto(int GoodsReceiptItemId, int GoodsReceiptId, int SupplierId, string SupplierName, string ReceiptNumber, DateTime ReceiptDate, string ItemType, string ItemDescription, decimal ReceivedQuantity, decimal StoredQuantity, decimal RemainingQuantity, string Unit, decimal UnitCost, int? RollCount, long? SupplierInvoiceLineId, string? ProductType = null, string? UnitCode = null, decimal? ItemCount = null, decimal? ReceivedItemCount = null);
public sealed record FabricDto(string SourceTable, int? FabricId, string? FabricCode, string? FabricName, decimal? FabricPrice, bool? IsActive, int? InventoryFabricCode, string? InventoryFabricName, string? Unit, string? Color, string? CatalogNumber, decimal? QuantityYard, decimal? QuantityInch, decimal? TotalRollCost, decimal? PricePerYard, decimal? PricePerInch, decimal? UsedQuantity, decimal? AvailableQuantity);
public sealed record ReadyMadeProductDto(int ReadyMadeInventoryProductId, int ReadyMadeProductionOrderId, int ReadyMadeProductionOrderItemId, int ReadyMadeProductionOrderPieceInstanceId, int? ProductTypeId, string ProductionOrderNumber, string ProductionName, string PieceType, int PieceNumber, string TrackingCode, string? FabricCode, string? FabricType, string? FabricColor, string? CatalogNumber, string? FabricUnit, string? FabricWidth, string? FabricWidthUnit, decimal? ActualCost, decimal? SuggestedSellingPrice, string? MeasurementSnapshot, DateTime ReadyForSaleAt, string Status, string Source, string? Notes, bool IsActive, DateTime CreatedAt, string? ProductTypeName = null);
public sealed record ImportedReadyMadeProductDto(int ImportedReadyMadeProductId, string ProductName, string ProductType, string ProductCode, string Unit, decimal Quantity, decimal PurchasePrice, decimal SellingPrice, bool IsActive, decimal? AlertThreshold, string? Notes, string Category, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed class CreateImportedProductDto
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

    public int? GoodsReceiptItemId { get; init; }
    public Guid? StorageOperationId { get; init; }
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

    public int? GoodsReceiptItemId { get; init; }
    public Guid? StorageOperationId { get; init; }
}

public sealed class CreateToolOperationalIssueDto
{
    [Range(1, int.MaxValue)]
    public int InventoryItemId { get; init; }

    [Range(typeof(decimal), "0.000001", "1000000000")]
    public decimal Quantity { get; init; }

    [Range(typeof(decimal), "0.01", "1000000000")]
    public decimal? OfficialUnitCost { get; init; }

    [Required, StringLength(500)]
    public string OperationalReason { get; init; } = string.Empty;

    [StringLength(1000)]
    public string? Notes { get; init; }

    [Range(1, int.MaxValue)]
    public int? ConfirmedByUserId { get; init; }

    public Guid SourceOperationId { get; init; }
}

public sealed class CreateToolCustodyIssueDto
{
    [Range(1, int.MaxValue)]
    public int InventoryItemId { get; init; }

    [Range(typeof(decimal), "0.000001", "1000000000")]
    public decimal Quantity { get; init; }

    [Range(typeof(decimal), "0.01", "1000000000")]
    public decimal OfficialUnitCost { get; init; }

    [Required, StringLength(200)]
    public string BeneficiaryName { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string DestinationType { get; init; } = string.Empty;

    [Required, StringLength(200)]
    public string DestinationName { get; init; } = string.Empty;

    [Required, StringLength(500)]
    public string LoanReason { get; init; } = string.Empty;

    [StringLength(1000)]
    public string? Notes { get; init; }

    [Range(1, int.MaxValue)]
    public int? ConfirmedByUserId { get; init; }

    public Guid SourceOperationId { get; init; }
}

public sealed class ReturnToolCustodyDto
{
    [Range(1, long.MaxValue)]
    public long ToolIssuanceId { get; init; }

    [Range(typeof(decimal), "0.000001", "1000000000")]
    public decimal ReturnedQuantity { get; init; }

    [StringLength(1000)]
    public string? ReturnNotes { get; init; }

    [Range(1, int.MaxValue)]
    public int? ConfirmedByUserId { get; init; }

    public Guid SourceOperationId { get; init; }
}

public sealed class ReverseToolOperationalIssueDto
{
    [Range(1, long.MaxValue)]
    public long ToolIssuanceId { get; init; }

    [Required, StringLength(500)]
    public string ReversalReason { get; init; } = string.Empty;

    [Required, StringLength(1000)]
    public string Notes { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int? ReversedBy { get; init; }

    public Guid SourceOperationId { get; init; }
}

public sealed record ToolIssuanceResultDto(
    long ToolIssuanceId,
    int InventoryItemId,
    string IssueType,
    decimal Quantity,
    decimal OfficialUnitCost,
    decimal OperationalAmount,
    decimal PostingAmount,
    string Status,
    long? AccountingEventId,
    int? InventoryTransactionId,
    Guid SourceOperationId,
    DateTime CreatedAt,
    bool IsExisting = false);

public sealed record ToolIssuanceHistoryDto(
    long ToolIssuanceId,
    int InventoryItemId,
    string ItemCode,
    string ItemName,
    string IssueType,
    decimal Quantity,
    decimal OfficialUnitCost,
    decimal OperationalAmount,
    decimal PostingAmount,
    string Status,
    long? AccountingEventId,
    int? InventoryTransactionId,
    Guid SourceOperationId,
    DateTime CreatedAt,
    string? BeneficiaryName,
    string? DestinationType,
    string? DestinationName,
    string? OperationalReason,
    string? LoanReason);

public sealed record OpenToolCustodyDto(
    long ToolIssuanceId,
    int InventoryItemId,
    string ItemCode,
    string ItemName,
    decimal Quantity,
    decimal ReturnedQuantity,
    decimal OutstandingQuantity,
    string BeneficiaryName,
    string DestinationType,
    string DestinationName,
    string LoanReason,
    DateTime CreatedAt,
    Guid SourceOperationId);

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

    public int? GoodsReceiptItemId { get; init; }
    public Guid? StorageOperationId { get; init; }
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

    [StringLength(100)]
    public string? CatalogNumber { get; init; }

    [Required, StringLength(100)]
    public string ItemCode { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string FabricTypeCode { get; init; } = string.Empty;

    [StringLength(100)]
    public string? RollCode { get; init; }

    [StringLength(100)]
    public string? ColorValue { get; init; }

    [Range(typeof(decimal), "0.01", "100000")]
    public decimal FabricWidth { get; init; }

    [Range(1, 2)]
    public short UnitId { get; init; } = 1;

    [Required, StringLength(100)]
    public string OpposingLedgerAccountCode { get; init; } = string.Empty;

    public Guid SourceOperationId { get; init; }

    [MinLength(1)]
    public IReadOnlyList<ReceiveFabricInventoryRollDto>? Rolls { get; init; }
}

public sealed class ReceiveFabricInventoryRollDto
{
    [Required, StringLength(100)]
    public string FabricCode { get; init; } = string.Empty;

    [StringLength(100)]
    public string? RollCode { get; init; }

    [StringLength(100)]
    public string? ColorValue { get; init; }

    [Range(typeof(decimal), "0.000001", "1000000")]
    public decimal Quantity { get; init; }
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

public sealed class ConsumeFabricCodeInventoryDto
{
    [Required, StringLength(100)]
    public string FabricCode { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int? OrderItemId { get; init; }

    [Range(1, int.MaxValue)]
    public int? PieceId { get; init; }

    [Range(1, int.MaxValue)]
    public int? ReadyMadeProductionOrderItemId { get; init; }

    [Range(1, int.MaxValue)]
    public int? ReadyMadeProductionOrderPieceInstanceId { get; init; }

    [Range(typeof(decimal), "0.000001", "36000000000")]
    public decimal QuantityInches { get; init; }

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

public sealed class CreateGoodsReceiptDto
{
    public int SupplierId { get; init; }
    public int? PurchaseOrderId { get; init; }
    public int? WarehouseId { get; init; }
    public string ReceiptNumber { get; init; } = string.Empty;
    public DateTime ReceiptDate { get; init; } = DateTime.UtcNow;
    public string? Notes { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public Guid SourceOperationId { get; init; }
    public IReadOnlyList<CreateGoodsReceiptItemDto> Items { get; init; } = [];
}

public sealed class CreateGoodsReceiptItemDto
{
    public int? InventoryItemId { get; init; }
    public string? ItemDescription { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitCost { get; init; }
    public int? RollCount { get; init; }
    public int? SupplierInvoiceLineId { get; init; }

    public string? ItemType { get; init; }
    public string? ProductType { get; init; }
    public string? UnitCode { get; init; }
    public decimal? ItemCount { get; init; }
    public decimal? ReceivedItemCount { get; init; }
    public string? RollCode { get; init; }
}

public sealed record GoodsReceiptRuntimeResult(int GoodsReceiptId, string ReceiptNumber, bool IsExisting);

public sealed class ReverseGoodsReceiptDto
{
    public int GoodsReceiptId { get; init; }
    public Guid SourceOperationId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string ReversedBy { get; init; } = string.Empty;
}

public sealed record GoodsReceiptReversalResult(long GoodsReceiptReversalId, bool IsExisting);