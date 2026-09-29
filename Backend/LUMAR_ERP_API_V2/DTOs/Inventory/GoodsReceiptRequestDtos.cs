namespace LUMAR_ERP_API_V2.DTOs.Inventory;

public sealed record CreateGoodsReceiptRequestDto(int SupplierId, int? PurchaseOrderId, int WarehouseId, string ReceiptNumber, DateTime ReceiptDate, string? Notes, Guid SourceOperationId, IReadOnlyList<CreateGoodsReceiptItemDto> Items);

public sealed record ReverseGoodsReceiptRequestDto(int GoodsReceiptId, Guid SourceOperationId, string Reason);